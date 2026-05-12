// Vision page - 2D FFT and image processing
// v1.3

(function () {
    'use strict';

    const VERSION = 'v1.3';

    let originalImageData = null;
    let fftData = null;
    let _origW = 0, _origH = 0;  // pixel dimensions of the original canvas

    // ── Camera state ───────────────────────────────────────────────────────────
    let _cameraStream = null;
    let _cameraRafId = null;

    // ── Live preview state ───────────────────────────────────────────────────
    let _liveRafId = null;
    let _liveFilter = 'none';
    let _liveOrigId = null, _liveFftId = null, _liveResultId = null;
    let _liveFftThrottle = 0;  // timestamp of last FFT update

    // ── Lightbox edge-detection state ──────────────────────────────────────────
    let _lbEdgeCanvas = null;   // overlay canvas we inject into lightbox
    let _lbEdgeActive = false;
    let _lbEdgeRafId = null;    // for live camera/video frames

    // ── Utility ────────────────────────────────────────────────────────────────

    function getCanvas(id) { return document.getElementById(id); }
    function getCtx(id) { const c = getCanvas(id); return c ? c.getContext('2d') : null; }

    // Force every result/FFT canvas to have the same CSS display size as the original.
    // This prevents mobile reflow from showing different heights for portrait images.
    function syncCanvasSizes(...ids) {
        if (!_origW || !_origH) return;
        for (const id of ids) {
            const c = getCanvas(id);
            if (!c) continue;
            c.style.width  = _origW + 'px';
            c.style.height = _origH + 'px';
        }
    }

    function resizeAndDraw(canvas, img) {
        const maxW = canvas.parentElement ? canvas.parentElement.clientWidth || 512 : 512;
        const scale = Math.min(maxW / img.width, 512 / img.height, 1);
        canvas.width = Math.round(img.width * scale);
        canvas.height = Math.round(img.height * scale);
        _origW = canvas.width;
        _origH = canvas.height;
        // Pin CSS size on the original canvas too so max-width:100% doesn't further scale it.
        canvas.style.width  = _origW + 'px';
        canvas.style.height = _origH + 'px';
        const ctx = canvas.getContext('2d');
        ctx.drawImage(img, 0, 0, canvas.width, canvas.height);
        return ctx.getImageData(0, 0, canvas.width, canvas.height);
    }

    // ── Complex FFT (Cooley-Tukey, radix-2) ───────────────────────────────────

    function nextPow2(n) {
        let p = 1;
        while (p < n) p <<= 1;
        return p;
    }

    function fft1d(re, im) {
        const n = re.length;
        if (n <= 1) return;
        // bit-reversal permutation
        let j = 0;
        for (let i = 1; i < n; i++) {
            let bit = n >> 1;
            for (; j & bit; bit >>= 1) j ^= bit;
            j ^= bit;
            if (i < j) {
                [re[i], re[j]] = [re[j], re[i]];
                [im[i], im[j]] = [im[j], im[i]];
            }
        }
        // butterfly
        for (let len = 2; len <= n; len <<= 1) {
            const ang = -2 * Math.PI / len;
            const wRe = Math.cos(ang), wIm = Math.sin(ang);
            for (let i = 0; i < n; i += len) {
                let uRe = 1, uIm = 0;
                for (let k = 0; k < len / 2; k++) {
                    const tRe = uRe * re[i + k + len / 2] - uIm * im[i + k + len / 2];
                    const tIm = uRe * im[i + k + len / 2] + uIm * re[i + k + len / 2];
                    re[i + k + len / 2] = re[i + k] - tRe;
                    im[i + k + len / 2] = im[i + k] - tIm;
                    re[i + k] += tRe;
                    im[i + k] += tIm;
                    const nextURe = uRe * wRe - uIm * wIm;
                    uIm = uRe * wIm + uIm * wRe;
                    uRe = nextURe;
                }
            }
        }
    }

    function fft2d(gray, w, h) {
        const W = nextPow2(w), H = nextPow2(h);
        // Build padded arrays
        const re = new Float64Array(W * H);
        const im = new Float64Array(W * H);
        for (let y = 0; y < h; y++)
            for (let x = 0; x < w; x++)
                re[y * W + x] = gray[y * w + x];

        // Row FFTs
        const rowRe = new Float64Array(W);
        const rowIm = new Float64Array(W);
        for (let y = 0; y < H; y++) {
            for (let x = 0; x < W; x++) { rowRe[x] = re[y * W + x]; rowIm[x] = im[y * W + x]; }
            fft1d(rowRe, rowIm);
            for (let x = 0; x < W; x++) { re[y * W + x] = rowRe[x]; im[y * W + x] = rowIm[x]; }
        }
        // Column FFTs
        const colRe = new Float64Array(H);
        const colIm = new Float64Array(H);
        for (let x = 0; x < W; x++) {
            for (let y = 0; y < H; y++) { colRe[y] = re[y * W + x]; colIm[y] = im[y * W + x]; }
            fft1d(colRe, colIm);
            for (let y = 0; y < H; y++) { re[y * W + x] = colRe[y]; im[y * W + x] = colIm[y]; }
        }
        return { re, im, W, H };
    }

    function ifft2d(re, im, W, H) {
        // conjugate → forward FFT → conjugate → /N
        const N = W * H;
        for (let i = 0; i < N; i++) im[i] = -im[i];
        const res = fft2d_from(re, im, W, H);
        for (let i = 0; i < N; i++) { res.re[i] /= N; res.im[i] = -res.im[i] / N; }
        return res;
    }

    function fft2d_from(reIn, imIn, W, H) {
        const re = reIn.slice();
        const im = imIn.slice();
        const rowRe = new Float64Array(W), rowIm = new Float64Array(W);
        for (let y = 0; y < H; y++) {
            for (let x = 0; x < W; x++) { rowRe[x] = re[y * W + x]; rowIm[x] = im[y * W + x]; }
            fft1d(rowRe, rowIm);
            for (let x = 0; x < W; x++) { re[y * W + x] = rowRe[x]; im[y * W + x] = rowIm[x]; }
        }
        const colRe = new Float64Array(H), colIm = new Float64Array(H);
        for (let x = 0; x < W; x++) {
            for (let y = 0; y < H; y++) { colRe[y] = re[y * W + x]; colIm[y] = im[y * W + x]; }
            fft1d(colRe, colIm);
            for (let y = 0; y < H; y++) { re[y * W + x] = colRe[y]; im[y * W + x] = colIm[y]; }
        }
        return { re, im, W, H };
    }

    // ── Grayscale extraction ───────────────────────────────────────────────────

    function toGray(imageData) {
        const d = imageData.data, n = imageData.width * imageData.height;
        const g = new Float64Array(n);
        for (let i = 0; i < n; i++)
            g[i] = 0.299 * d[i * 4] + 0.587 * d[i * 4 + 1] + 0.114 * d[i * 4 + 2];
        return g;
    }

    // ── FFT magnitude display (log-scaled, DC centered) ───────────────────────

    function drawMagnitude(canvasId, re, im, W, H, dispW, dispH) {
        const canvas = getCanvas(canvasId);
        if (!canvas) return;
        canvas.width = dispW; canvas.height = dispH;
        const ctx = canvas.getContext('2d');
        const out = ctx.createImageData(dispW, dispH);

        // Compute magnitude with DC shift (fftshift)
        const mag = new Float64Array(W * H);
        let maxM = 0;
        for (let y = 0; y < H; y++)
            for (let x = 0; x < W; x++) {
                const idx = y * W + x;
                const m = Math.log1p(Math.sqrt(re[idx] * re[idx] + im[idx] * im[idx]));
                mag[idx] = m;
                if (m > maxM) maxM = m;
            }

        for (let py = 0; py < dispH; py++) {
            for (let px = 0; px < dispW; px++) {
                // Map display pixel → FFT pixel with DC shift
                const fx = ((Math.round(px * W / dispW) + W / 2) % W);
                const fy = ((Math.round(py * H / dispH) + H / 2) % H);
                const v = Math.round(255 * mag[fy * W + fx] / (maxM || 1));
                const oi = (py * dispW + px) * 4;
                out.data[oi] = v; out.data[oi + 1] = v; out.data[oi + 2] = v; out.data[oi + 3] = 255;
            }
        }
        ctx.putImageData(out, 0, 0);
    }

    // ── Filter helpers ─────────────────────────────────────────────────────────

    function applyFreqFilter(re, im, W, H, filterFn) {
        const reF = re.slice(), imF = im.slice();
        for (let y = 0; y < H; y++) {
            for (let x = 0; x < W; x++) {
                // shifted coords
                const fx = x < W / 2 ? x : x - W;
                const fy = y < H / 2 ? y : y - H;
                const dist = Math.sqrt((fx / (W / 2)) ** 2 + (fy / (H / 2)) ** 2); // 0..~1.4
                const gain = filterFn(dist, fx, fy, W, H);
                const idx = y * W + x;
                reF[idx] *= gain;
                imF[idx] *= gain;
            }
        }
        return { re: reF, im: imF };
    }

    const FILTERS = {
        none: null,
        lowpass: (r) => r < 0.3 ? 1 : 0,
        lowpass_soft: (r) => Math.max(0, 1 - r / 0.4),
        highpass: (r) => r > 0.15 ? 1 : 0,
        bandpass: (r) => (r > 0.1 && r < 0.4) ? 1 : 0,
        edge_sobel: (r) => Math.min(1, r * 1.5),
    };

    // ── Spatial (pixel) filters ────────────────────────────────────────────────

    function convolve(imageData, kernel, kSize) {
        const w = imageData.width, h = imageData.height;
        const src = imageData.data;
        const out = new Uint8ClampedArray(src.length);
        const half = Math.floor(kSize / 2);
        for (let y = 0; y < h; y++) {
            for (let x = 0; x < w; x++) {
                let r = 0, g = 0, b = 0;
                for (let ky = 0; ky < kSize; ky++) {
                    for (let kx = 0; kx < kSize; kx++) {
                        const sy = Math.min(h - 1, Math.max(0, y + ky - half));
                        const sx = Math.min(w - 1, Math.max(0, x + kx - half));
                        const si = (sy * w + sx) * 4;
                        const k = kernel[ky * kSize + kx];
                        r += src[si] * k;
                        g += src[si + 1] * k;
                        b += src[si + 2] * k;
                    }
                }
                const oi = (y * w + x) * 4;
                out[oi] = Math.min(255, Math.max(0, r));
                out[oi + 1] = Math.min(255, Math.max(0, g));
                out[oi + 2] = Math.min(255, Math.max(0, b));
                out[oi + 3] = src[oi + 3];
            }
        }
        return new ImageData(out, w, h);
    }

    function sobelEdge(imageData) {
        const w = imageData.width, h = imageData.height;
        const d = imageData.data;
        const out = new Uint8ClampedArray(d.length);
        const gx = [-1, 0, 1, -2, 0, 2, -1, 0, 1];
        const gy = [-1, -2, -1, 0, 0, 0, 1, 2, 1];
        for (let y = 0; y < h; y++) {
            for (let x = 0; x < w; x++) {
                let sx = 0, sy = 0;
                for (let ky = 0; ky < 3; ky++) {
                    for (let kx = 0; kx < 3; kx++) {
                        const ny = Math.min(h - 1, Math.max(0, y + ky - 1));
                        const nx = Math.min(w - 1, Math.max(0, x + kx - 1));
                        const lum = 0.299 * d[(ny * w + nx) * 4] + 0.587 * d[(ny * w + nx) * 4 + 1] + 0.114 * d[(ny * w + nx) * 4 + 2];
                        sx += lum * gx[ky * 3 + kx];
                        sy += lum * gy[ky * 3 + kx];
                    }
                }
                const mag = Math.min(255, Math.sqrt(sx * sx + sy * sy));
                const oi = (y * w + x) * 4;
                out[oi] = mag; out[oi + 1] = mag; out[oi + 2] = mag; out[oi + 3] = 255;
            }
        }
        return new ImageData(out, w, h);
    }

    function grayscale(imageData) {
        const d = imageData.data.slice();
        for (let i = 0; i < d.length; i += 4) {
            const g = 0.299 * d[i] + 0.587 * d[i + 1] + 0.114 * d[i + 2];
            d[i] = d[i + 1] = d[i + 2] = g;
        }
        return new ImageData(d, imageData.width, imageData.height);
    }

    function sharpen(imageData) {
        return convolve(imageData, [0, -1, 0, -1, 5, -1, 0, -1, 0], 3);
    }

    function blur(imageData) {
        const k = [1, 2, 1, 2, 4, 2, 1, 2, 1].map(v => v / 16);
        return convolve(imageData, k, 3);
    }

    function emboss(imageData) {
        return convolve(imageData, [-2, -1, 0, -1, 1, 1, 0, 1, 2], 3);
    }

    function invert(imageData) {
        const d = imageData.data.slice();
        for (let i = 0; i < d.length; i += 4) {
            d[i] = 255 - d[i]; d[i + 1] = 255 - d[i + 1]; d[i + 2] = 255 - d[i + 2];
        }
        return new ImageData(d, imageData.width, imageData.height);
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    window.visionGame = {
        version: VERSION,

        loadImageFromDataUrl(dataUrl, origCanvasId, fftCanvasId) {
            return new Promise((resolve, reject) => {
                const img = new Image();
                img.onload = () => {
                    const canvas = getCanvas(origCanvasId);
                    if (!canvas) { reject('canvas not found'); return; }
                    originalImageData = resizeAndDraw(canvas, img);
                    fftData = this._computeFFT(originalImageData);
                    drawMagnitude(fftCanvasId, fftData.re, fftData.im, fftData.W, fftData.H, canvas.width, canvas.height);
                    resolve({ width: canvas.width, height: canvas.height });
                };
                img.onerror = reject;
                img.src = dataUrl;
            });
        },

        async pasteFromClipboard(origCanvasId, fftCanvasId) {
            try {
                const items = await navigator.clipboard.read();
                for (const item of items) {
                    const type = item.types.find(t => t.startsWith('image/'));
                    if (type) {
                        const blob = await item.getType(type);
                        const url = URL.createObjectURL(blob);
                        const result = await this.loadImageFromDataUrl(url, origCanvasId, fftCanvasId);
                        URL.revokeObjectURL(url);
                        return result;
                    }
                }
                return null;
            } catch (e) {
                console.error('[Vision] clipboard error', e);
                return null;
            }
        },

        _computeFFT(imageData) {
            const gray = toGray(imageData);
            return fft2d(gray, imageData.width, imageData.height);
        },

        applyFilter(filterName, fftCanvasId, resultCanvasId) {
            if (!originalImageData) return false;
            if (!fftData) fftData = this._computeFFT(originalImageData);

            const { W, H } = fftData;
            const dispW = originalImageData.width, dispH = originalImageData.height;

            // Determine if this is a spatial or frequency-domain filter
            const spatialFilters = {
                grayscale, sharpen, blur, edge_sobel_spatial: sobelEdge, emboss, invert
            };

            const _sync = () => syncCanvasSizes(fftCanvasId, resultCanvasId);

            if (filterName === 'none') {
                // Show original in result
                const rc = getCanvas(resultCanvasId);
                if (rc) {
                    rc.width = dispW; rc.height = dispH;
                    rc.getContext('2d').putImageData(originalImageData, 0, 0);
                }
                // Restore full FFT magnitude
                drawMagnitude(fftCanvasId, fftData.re, fftData.im, W, H, dispW, dispH);
                _sync();
                return true;
            }

            if (spatialFilters[filterName]) {
                const result = spatialFilters[filterName](originalImageData);
                const rc = getCanvas(resultCanvasId);
                if (rc) {
                    rc.width = dispW; rc.height = dispH;
                    rc.getContext('2d').putImageData(result, 0, 0);
                }
                // Compute and show FFT of result
                const resultGray = toGray(result);
                const resFft = fft2d(resultGray, result.width, result.height);
                drawMagnitude(fftCanvasId, resFft.re, resFft.im, resFft.W, resFft.H, dispW, dispH);
                _sync();
                return true;
            }

            // Frequency-domain filter
            const freqFilterFn = FILTERS[filterName];
            if (!freqFilterFn) return false;

            const filtered = applyFreqFilter(fftData.re, fftData.im, W, H, freqFilterFn);
            drawMagnitude(fftCanvasId, filtered.re, filtered.im, W, H, dispW, dispH);

            // IFFT back to spatial
            const inv = ifft2d(filtered.re, filtered.im, W, H);
            const rc = getCanvas(resultCanvasId);
            if (!rc) return true;
            rc.width = dispW; rc.height = dispH;
            const ctx = rc.getContext('2d');
            const out = ctx.createImageData(dispW, dispH);
            // Find max for normalization
            let maxV = 0;
            for (let y = 0; y < dispH; y++)
                for (let x = 0; x < dispW; x++) {
                    const v = Math.abs(inv.re[y * W + x]);
                    if (v > maxV) maxV = v;
                }
            for (let y = 0; y < dispH; y++) {
                for (let x = 0; x < dispW; x++) {
                    const v = Math.min(255, Math.round(Math.abs(inv.re[y * W + x]) * 255 / (maxV || 1)));
                    const oi = (y * dispW + x) * 4;
                    out.data[oi] = v; out.data[oi + 1] = v; out.data[oi + 2] = v; out.data[oi + 3] = 255;
                }
            }
            ctx.putImageData(out, 0, 0);
            _sync();
            return true;
        },

        hasImage() { return originalImageData !== null; },
        clearImage() { originalImageData = null; fftData = null; _origW = 0; _origH = 0; },

        // ── Camera ────────────────────────────────────────────────────────────

        // Returns array of { deviceId, label } for all video input devices.
        // Call this only AFTER getUserMedia has already been granted (labels are empty before that).
        async enumerateCameras() {
            const devices = await navigator.mediaDevices.enumerateDevices();
            const cameras = devices
                .filter(d => d.kind === 'videoinput')
                .map((d, i) => ({
                    deviceId: d.deviceId,
                    label: d.label || `Camera ${i + 1}`
                }));
            console.log('[Vision] cameras found:', cameras.map(c => c.label));
            return cameras;
        },

        // Returns the deviceId of the currently active camera track, or empty string.
        getActiveCameraDeviceId() {
            if (!_cameraStream) return '';
            const tracks = _cameraStream.getVideoTracks();
            return tracks.length > 0 ? (tracks[0].getSettings().deviceId || '') : '';
        },

        async startCamera(videoElId, deviceIdOrFacingMode) {
            await this.stopCamera();
            const videoEl = document.getElementById(videoElId);
            if (!videoEl) return false;
            try {
                // If the caller passed a real deviceId (36-char hex or long string), use it exactly.
                // Otherwise treat it as a facingMode hint ('user'/'environment').
                let videoConstraint;
                const looksLikeDeviceId = deviceIdOrFacingMode &&
                    deviceIdOrFacingMode !== 'user' &&
                    deviceIdOrFacingMode !== 'environment';

                if (looksLikeDeviceId) {
                    videoConstraint = { deviceId: { exact: deviceIdOrFacingMode } };
                } else {
                    // facingMode is a hint only – don't use 'exact' so desktop doesn't fail
                    videoConstraint = deviceIdOrFacingMode
                        ? { facingMode: deviceIdOrFacingMode }
                        : true;
                }

                _cameraStream = await navigator.mediaDevices.getUserMedia({
                    video: videoConstraint,
                    audio: false
                });
                videoEl.srcObject = _cameraStream;
                await videoEl.play();
                return true;
            } catch (e) {
                console.error('[Vision] camera error', e);
                return false;
            }
        },

        async stopCamera(videoElId) {
            if (_cameraRafId) { cancelAnimationFrame(_cameraRafId); _cameraRafId = null; }
            this.stopLivePreview();
            if (_cameraStream) {
                _cameraStream.getTracks().forEach(t => t.stop());
                _cameraStream = null;
            }
            if (videoElId) {
                const v = document.getElementById(videoElId);
                if (v) { v.srcObject = null; v.pause(); }
            }
        },

        // Snapshot: grab current video frame → load as image into the three-panel pipeline
        captureFrame(videoElId, origCanvasId, fftCanvasId) {
            const video = document.getElementById(videoElId);
            if (!video || video.readyState < 2) return false;
            const w = video.videoWidth, h = video.videoHeight;
            const tmp = document.createElement('canvas');
            tmp.width = w; tmp.height = h;
            tmp.getContext('2d').drawImage(video, 0, 0, w, h);

            const canvas = getCanvas(origCanvasId);
            if (!canvas) return false;
            const maxSz = 512;
            const scale = Math.min(maxSz / w, maxSz / h, 1);
            canvas.width = Math.round(w * scale);
            canvas.height = Math.round(h * scale);
            _origW = canvas.width;
            _origH = canvas.height;
            canvas.style.width  = _origW + 'px';
            canvas.style.height = _origH + 'px';
            const ctx = canvas.getContext('2d');
            ctx.drawImage(tmp, 0, 0, canvas.width, canvas.height);
            originalImageData = ctx.getImageData(0, 0, canvas.width, canvas.height);
            fftData = this._computeFFT(originalImageData);
            drawMagnitude(fftCanvasId, fftData.re, fftData.im, fftData.W, fftData.H, canvas.width, canvas.height);
            syncCanvasSizes(fftCanvasId, 'visionResultCanvas');
            return true;
        },

        // ── Live camera preview ────────────────────────────────────────────────

        startLivePreview(videoElId, origCanvasId, fftCanvasId, resultCanvasId, filterName) {
            this.stopLivePreview();
            _liveFilter  = filterName || 'none';
            _liveOrigId  = origCanvasId;
            _liveFftId   = fftCanvasId;
            _liveResultId = resultCanvasId;
            const self = this;
            const video = document.getElementById(videoElId);
            if (!video) return;

            // Spatial filters that are fast enough to run every frame
            const spatialFilters = {
                grayscale, sharpen, blur, edge_sobel_spatial: sobelEdge, emboss, invert
            };

            function tick(now) {
                if (!_liveRafId) return;  // stopped
                _liveRafId = requestAnimationFrame(tick);

                if (!video || video.readyState < 2 || video.paused) return;

                const vw = video.videoWidth, vh = video.videoHeight;
                if (!vw || !vh) return;

                // Scale to processing size once per stream (stable dimensions)
                const maxSz = 512;
                const scale = Math.min(maxSz / vw, maxSz / vh, 1);
                const pw = Math.round(vw * scale), ph = Math.round(vh * scale);

                // Draw video frame → original canvas
                const origCanvas = getCanvas(_liveOrigId);
                if (!origCanvas) return;
                if (origCanvas.width !== pw || origCanvas.height !== ph) {
                    origCanvas.width = pw; origCanvas.height = ph;
                    _origW = pw; _origH = ph;
                    origCanvas.style.width  = pw + 'px';
                    origCanvas.style.height = ph + 'px';
                    syncCanvasSizes(_liveFftId, _liveResultId);
                }
                origCanvas.getContext('2d').drawImage(video, 0, 0, pw, ph);
                originalImageData = origCanvas.getContext('2d').getImageData(0, 0, pw, ph);

                // Apply filter to result canvas every frame (spatial only; freq-domain on throttle)
                const resultCanvas = getCanvas(_liveResultId);
                if (resultCanvas) {
                    if (spatialFilters[_liveFilter]) {
                        const filtered = spatialFilters[_liveFilter](originalImageData);
                        resultCanvas.width = pw; resultCanvas.height = ph;
                        resultCanvas.getContext('2d').putImageData(filtered, 0, 0);
                    } else if (_liveFilter === 'none') {
                        resultCanvas.width = pw; resultCanvas.height = ph;
                        resultCanvas.getContext('2d').putImageData(originalImageData, 0, 0);
                    } else {
                        // Frequency-domain: throttle to ~2fps (expensive)
                        if (now - _liveFftThrottle > 500) {
                            _liveFftThrottle = now;
                            const fd = self._computeFFT(originalImageData);
                            const freqFn = FILTERS[_liveFilter];
                            if (freqFn) {
                                const filtered = applyFreqFilter(fd.re, fd.im, fd.W, fd.H, freqFn);
                                drawMagnitude(_liveFftId, filtered.re, filtered.im, fd.W, fd.H, pw, ph);
                                const inv = ifft2d(filtered.re, filtered.im, fd.W, fd.H);
                                resultCanvas.width = pw; resultCanvas.height = ph;
                                const ctx = resultCanvas.getContext('2d');
                                const out = ctx.createImageData(pw, ph);
                                let maxV = 0;
                                for (let i = 0; i < ph; i++)
                                    for (let j = 0; j < pw; j++) {
                                        const v = Math.abs(inv.re[i * fd.W + j]);
                                        if (v > maxV) maxV = v;
                                    }
                                for (let i = 0; i < ph; i++)
                                    for (let j = 0; j < pw; j++) {
                                        const v = Math.min(255, Math.round(Math.abs(inv.re[i * fd.W + j]) * 255 / (maxV || 1)));
                                        const oi = (i * pw + j) * 4;
                                        out.data[oi] = v; out.data[oi+1] = v; out.data[oi+2] = v; out.data[oi+3] = 255;
                                    }
                                ctx.putImageData(out, 0, 0);
                                return;  // FFT canvas already updated above
                            }
                        } else { return; }
                    }
                }

                // FFT magnitude: update at ~2fps to avoid jank
                if (now - _liveFftThrottle > 500) {
                    _liveFftThrottle = now;
                    fftData = self._computeFFT(originalImageData);
                    drawMagnitude(_liveFftId, fftData.re, fftData.im, fftData.W, fftData.H, pw, ph);
                }
            }

            _liveRafId = requestAnimationFrame(tick);
        },

        stopLivePreview() {
            if (_liveRafId) { cancelAnimationFrame(_liveRafId); _liveRafId = null; }
        },

        // Called after stopLivePreview: treats whatever is already drawn in origCanvas as the
        // current image and (re)computes fftData so the static pipeline can work on it.
        freezeLiveFrame(origCanvasId, fftCanvasId) {
            const canvas = getCanvas(origCanvasId);
            if (!canvas || canvas.width < 2) return false;
            originalImageData = canvas.getContext('2d').getImageData(0, 0, canvas.width, canvas.height);
            fftData = this._computeFFT(originalImageData);
            drawMagnitude(fftCanvasId, fftData.re, fftData.im, fftData.W, fftData.H,
                originalImageData.width, originalImageData.height);
            syncCanvasSizes(fftCanvasId, 'visionResultCanvas');
            return true;
        },

        setLiveFilter(filterName) {
            _liveFilter = filterName || 'none';
            _liveFftThrottle = 0;  // force immediate FFT update on next tick
        },

        // ── Lightbox edge detection ────────────────────────────────────────────

        // Toggle Sobel edge overlay in the lightbox.
        // Works for <img id="imageMain"> and a paused <video id="myVideo">.
        // Returns new active state.
        lightboxToggleEdge(imageElId, videoElId) {
            _lbEdgeActive = !_lbEdgeActive;
            if (!_lbEdgeActive) {
                this._lbEdgeStop();
                return false;
            }
            this._lbEdgeRender(imageElId, videoElId);
            return true;
        },

        // Called when lightbox item changes — clear overlay so stale edge doesn't show.
        lightboxEdgeClear() {
            _lbEdgeActive = false;
            this._lbEdgeStop();
        },

        _lbEdgeStop() {
            if (_lbEdgeRafId) { cancelAnimationFrame(_lbEdgeRafId); _lbEdgeRafId = null; }
            if (_lbEdgeCanvas) { _lbEdgeCanvas.remove(); _lbEdgeCanvas = null; }
        },

        _lbEdgeRender(imageElId, videoElId) {
            if (!_lbEdgeActive) return;

            const imgEl = document.getElementById(imageElId);
            const vidEl = document.getElementById(videoElId);

            // Choose source: visible image OR paused/playing video
            let source = null;
            if (imgEl && imgEl.naturalWidth > 0 && imgEl.style.display !== 'none' && imgEl.src && !imgEl.src.endsWith('null')) {
                source = imgEl;
            } else if (vidEl && vidEl.videoWidth > 0) {
                source = vidEl;
            }

            if (!source) {
                _lbEdgeRafId = requestAnimationFrame(() => this._lbEdgeRender(imageElId, videoElId));
                return;
            }

            const srcW = source.naturalWidth || source.videoWidth;
            const srcH = source.naturalHeight || source.videoHeight;

            // Compute the actual rendered rect of the source element relative to its parent.
            // getBoundingClientRect() gives the element layout box, but with object-fit:contain
            // a portrait image in a wide container has letterbar space — we must compute the
            // true content rect from the aspect ratio so the overlay isn't stretched wide.
            const parent = source.parentElement;
            if (parent) parent.style.position = 'relative';

            const parentRect = parent ? parent.getBoundingClientRect() : { left: 0, top: 0 };
            const elemRect = source.getBoundingClientRect();
            const elemW = elemRect.width;
            const elemH = elemRect.height;

            // Scale to fit inside element box while preserving aspect ratio (object-fit:contain)
            const fitScale = Math.min(elemW / srcW, elemH / srcH);
            const rendW = srcW * fitScale;
            const rendH = srcH * fitScale;
            // Centre the content rect within the element box
            const offLeft = (elemRect.left - parentRect.left) + (elemW - rendW) / 2;
            const offTop  = (elemRect.top  - parentRect.top)  + (elemH - rendH) / 2;
            const dispW = rendW;
            const dispH = rendH;

            // Create / reuse overlay canvas
            if (!_lbEdgeCanvas) {
                _lbEdgeCanvas = document.createElement('canvas');
                _lbEdgeCanvas.style.cssText =
                    'position:absolute;pointer-events:none;z-index:10;';
                if (parent) parent.appendChild(_lbEdgeCanvas);
            }
            // Position overlay exactly over the image/video, not the whole parent
            _lbEdgeCanvas.style.left   = offLeft + 'px';
            _lbEdgeCanvas.style.top    = offTop  + 'px';
            _lbEdgeCanvas.style.width  = dispW   + 'px';
            _lbEdgeCanvas.style.height = dispH   + 'px';

            const scale = Math.min(512 / srcW, 512 / srcH, 1);
            const procW = Math.round(srcW * scale);
            const procH = Math.round(srcH * scale);

            // Draw source into offscreen canvas at processing resolution
            const offscreen = document.createElement('canvas');
            offscreen.width = procW; offscreen.height = procH;
            offscreen.getContext('2d').drawImage(source, 0, 0, procW, procH);
            const imgData = offscreen.getContext('2d').getImageData(0, 0, procW, procH);

            const edged = sobelEdge(imgData);

            _lbEdgeCanvas.width = procW;
            _lbEdgeCanvas.height = procH;
            _lbEdgeCanvas.getContext('2d').putImageData(edged, 0, 0);

            // For live video keep refreshing; for still image once is enough.
            const isLive = source instanceof HTMLVideoElement && !source.paused;
            if (isLive) {
                _lbEdgeRafId = requestAnimationFrame(() => this._lbEdgeRender(imageElId, videoElId));
            }
        },
    };

    console.log(`[Vision ${VERSION}] JS loaded`);
})();
