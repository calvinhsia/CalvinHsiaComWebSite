// audio-game.js - Audio recording, playback, sin-wave mixing, FFT filtering
'use strict';

const AUDIO_VERSION = 'v5';

window.audioGame = (() => {
    let audioContext = null;
    let mediaRecorder = null;
    let recordedChunks = [];
    let recordedBuffer = null;      // decoded AudioBuffer of the recording

    // Sin-wave nodes (always routed to speakers AND optionally into the mix bus)
    let sinOscillator = null;
    let sinGainNode = null;         // controls speaker volume
    let sinMixGainNode = null;      // routes sin into the recording mix bus
    let sinFrequency = 440;
    let sinAmplitude = 0.3;
    let micGainNode = null;         // scales mic level in the recording mix independently
    let micGainValue = 1.0;
    let isRecording = false;
    let isPlayingSin = false;
    let dotnetRef = null;

    // The mix bus: MediaStreamDestinationNode used while recording.
    // All sources that should appear in the recording connect here.
    let mixDestination = null;

    function getCtx() {
        if (!audioContext) {
            audioContext = new (window.AudioContext || window.webkitAudioContext)();
        }
        return audioContext;
    }

    function init(ref) {
        dotnetRef = ref;
        // Detect platform capabilities upfront
        const ua = navigator.userAgent;
        const isIOS = /iPad|iPhone|iPod/.test(ua) || (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);
        const hasMR = typeof MediaRecorder !== 'undefined';
        const hasDisplay = !!navigator.mediaDevices?.getDisplayMedia;
        console.log(`[Audio ${AUDIO_VERSION}] init iOS=${isIOS} MediaRecorder=${hasMR} getDisplayMedia=${hasDisplay}`);
    }

    // Pick the best supported mime type for MediaRecorder across platforms:
    // - Chrome/Edge/Firefox (desktop/Android): audio/webm;codecs=opus
    // - Safari 14.1+ / iOS 14.5+:             audio/mp4
    function _bestMimeType() {
        const candidates = [
            'audio/webm;codecs=opus',
            'audio/webm',
            'audio/mp4;codecs=mp4a.40.2',
            'audio/mp4',
            'audio/ogg;codecs=opus',
            '',
        ];
        for (const t of candidates) {
            if (t === '' || MediaRecorder.isTypeSupported(t)) return t;
        }
        return '';
    }

    // ── Sin-wave tone ────────────────────────────────────────────────────────
    // The oscillator is connected to TWO places simultaneously:
    //   1. ctx.destination  → you hear it through speakers/headphones
    //   2. mixDestination   → it is digitally captured in the recording
    // Connection to mixDestination is made (or updated) whenever recording starts.

    function startSinWave(freq, amp) {
        const ctx = getCtx();
        if (ctx.state === 'suspended') ctx.resume();
        sinFrequency = freq || sinFrequency;
        sinAmplitude = amp !== undefined ? amp : sinAmplitude;
        if (sinOscillator) _teardownSinNodes();

        // Gain node → speakers
        sinGainNode = ctx.createGain();
        sinGainNode.gain.value = sinAmplitude;
        sinGainNode.connect(ctx.destination);

        // Separate gain node → recording mix bus (connected lazily when recording starts)
        sinMixGainNode = ctx.createGain();
        sinMixGainNode.gain.value = sinAmplitude;
        if (mixDestination) {
            sinMixGainNode.connect(mixDestination);
        }

        sinOscillator = ctx.createOscillator();
        sinOscillator.type = 'sine';
        sinOscillator.frequency.value = sinFrequency;
        sinOscillator.connect(sinGainNode);
        sinOscillator.connect(sinMixGainNode);
        sinOscillator.start();
        isPlayingSin = true;
        console.log(`[Audio ${AUDIO_VERSION}] sin started freq=${sinFrequency} amp=${sinAmplitude}`);
    }

    function _teardownSinNodes() {
        if (sinOscillator) {
            try { sinOscillator.stop(); } catch (_) {}
            sinOscillator.disconnect();
            sinOscillator = null;
        }
        if (sinGainNode)    { sinGainNode.disconnect();    sinGainNode    = null; }
        if (sinMixGainNode) { sinMixGainNode.disconnect(); sinMixGainNode = null; }
    }

    function stopSinWave() {
        _teardownSinNodes();
        isPlayingSin = false;
        console.log(`[Audio ${AUDIO_VERSION}] sin stopped`);
    }

    function setSinFrequency(freq) {
        sinFrequency = freq;
        if (sinOscillator) sinOscillator.frequency.value = freq;
    }

    function setSinAmplitude(amp) {
        sinAmplitude = amp;
        if (sinGainNode)    sinGainNode.gain.value    = amp;
        if (sinMixGainNode) sinMixGainNode.gain.value = amp;
    }

    function setMicGain(gain) {
        micGainValue = gain;
        if (micGainNode) micGainNode.gain.value = gain;
    }

    // ── Recording ────────────────────────────────────────────────────────────
    // Architecture:
    //
    //  Microphone/display capture ──► MediaStreamSourceNode ──┐
    //                                                          ├─► mixDestination (MediaStreamDestinationNode)
    //  OscillatorNode ──────────────► sinMixGainNode ─────────┘        │
    //                                                                   ▼
    //                                                            MediaRecorder
    //
    // The sin wave is also connected to ctx.destination so the user hears it.

    async function startRecording(sourceType, mixSin) {
        if (typeof MediaRecorder === 'undefined') {
            throw new Error('MediaRecorder is not supported on this browser/platform.');
        }
        const ctx = getCtx();
        if (ctx.state === 'suspended') ctx.resume();

        // Create the mix bus for this recording session
        mixDestination = ctx.createMediaStreamDestination();

        // ── Input source ──
        let inputStream;
        if (sourceType === 'display') {
            if (!navigator.mediaDevices?.getDisplayMedia) {
                throw new Error('Screen/tab capture (getDisplayMedia) is not supported on this browser. Use Microphone instead.');
            }
            const displayStream = await navigator.mediaDevices.getDisplayMedia({ video: true, audio: true });
            const audioTracks = displayStream.getAudioTracks();
            if (audioTracks.length === 0) {
                displayStream.getTracks().forEach(t => t.stop());
                throw new Error('No audio track in display capture. Make sure to check "Share audio".');
            }
            displayStream.getVideoTracks().forEach(t => t.stop());
            inputStream = new MediaStream(audioTracks);
        } else {
            // Disable all browser audio processing that fights the sin wave:
            // - echoCancellation: removes sounds that match speaker output (kills the sin tone)
            // - noiseSuppression: treats steady tones as noise and attenuates them
            // - autoGainControl: adjusts mic level dynamically, fights our manual mix
            inputStream = await navigator.mediaDevices.getUserMedia({
                audio: {
                    echoCancellation: false,
                    noiseSuppression: false,
                    autoGainControl: false,
                },
                video: false
            });
        }

        // Route input through a gain node into the mix bus so mic level is adjustable
        const micSource = ctx.createMediaStreamSource(inputStream);
        micGainNode = ctx.createGain();
        micGainNode.gain.value = micGainValue;
        micSource.connect(micGainNode);
        micGainNode.connect(mixDestination);

        // Route the sin oscillator into the mix bus (if it is playing)
        if (sinMixGainNode) {
            sinMixGainNode.connect(mixDestination);
        }

        recordedChunks = [];
        recordedBuffer = null;

        const mimeType = _bestMimeType();
        console.log(`[Audio ${AUDIO_VERSION}] using mimeType="${mimeType}"`);

        const mrOptions = mimeType ? { mimeType } : {};
        mediaRecorder = new MediaRecorder(mixDestination.stream, mrOptions);
        mediaRecorder.ondataavailable = e => { if (e.data.size > 0) recordedChunks.push(e.data); };
        mediaRecorder.onstop = async () => {
            // Clean up mic tracks
            inputStream.getTracks().forEach(t => t.stop());
            if (micGainNode) { micGainNode.disconnect(); micGainNode = null; }
            mixDestination = null;
            const blob = new Blob(recordedChunks, { type: mimeType || 'audio/webm' });
            const arrayBuffer = await blob.arrayBuffer();
            recordedBuffer = await ctx.decodeAudioData(arrayBuffer);
            console.log(`[Audio ${AUDIO_VERSION}] recording decoded: ${recordedBuffer.duration.toFixed(2)}s`);
            if (dotnetRef) {
                dotnetRef.invokeMethodAsync('OnRecordingStopped', recordedBuffer.duration);
            }
        };

        mediaRecorder.start(100);
        isRecording = true;
        console.log(`[Audio ${AUDIO_VERSION}] recording started (${sourceType}) - digital mix`);
    }

    function stopRecording() {
        if (mediaRecorder && mediaRecorder.state !== 'inactive') {
            mediaRecorder.stop();
        }
        isRecording = false;
        console.log(`[Audio ${AUDIO_VERSION}] recording stop requested`);
    }

    // ── Playback ─────────────────────────────────────────────────────────────

    let playbackSource = null;

    function playRecording() {
        if (!recordedBuffer) { console.warn('[Audio] No recording'); return; }
        const ctx = getCtx();
        if (ctx.state === 'suspended') ctx.resume();
        if (playbackSource) { playbackSource.stop(); playbackSource = null; }
        playbackSource = ctx.createBufferSource();
        playbackSource.buffer = recordedBuffer;
        playbackSource.connect(ctx.destination);
        playbackSource.start();
        console.log(`[Audio ${AUDIO_VERSION}] playback started`);
    }

    function stopPlayback() {
        if (playbackSource) {
            try { playbackSource.stop(); } catch (_) { }
            playbackSource = null;
        }
    }

    // ── FFT Notch Filter ─────────────────────────────────────────────────────
    // Chain one BiquadFilterNode per frequency in series:
    //   source → notch(f1) → notch(f2) → ... → destination/analyser

    function _buildNotchChain(ctx, freqs, q) {
        // freqs: number[] of Hz values; q: number
        const filters = freqs.map(f => {
            const n = ctx.createBiquadFilter();
            n.type = 'notch';
            n.frequency.value = f;
            n.Q.value = q || 30;
            return n;
        });
        // chain them: filters[0] → filters[1] → ...
        for (let i = 0; i < filters.length - 1; i++) filters[i].connect(filters[i + 1]);
        return filters; // [0] = input end, [last] = output end
    }

    function playRecordingFiltered(notchFreqs, notchQ) {
        if (!recordedBuffer) { console.warn('[Audio] No recording'); return; }
        const ctx = getCtx();
        if (ctx.state === 'suspended') ctx.resume();
        if (playbackSource) { try { playbackSource.stop(); } catch (_) { } playbackSource = null; }

        const freqs = (notchFreqs && notchFreqs.length) ? notchFreqs : [sinFrequency];
        const chain = _buildNotchChain(ctx, freqs, notchQ);

        playbackSource = ctx.createBufferSource();
        playbackSource.buffer = recordedBuffer;
        playbackSource.connect(chain[0]);
        chain[chain.length - 1].connect(ctx.destination);
        playbackSource.start();
        console.log(`[Audio ${AUDIO_VERSION}] filtered playback freqs=[${freqs}] Q=${notchQ}`);
    }

    // ── FFT magnitude spectrum (for visualisation) ────────────────────────────
    // Returns Float32Array of dB values for the current playback via AnalyserNode.
    // We expose a simple snapshot approach: connect an analyser during playback.

    let analyser = null;
    let analyserData = null;

    function playRecordingWithAnalyser(filtered, notchFreqs, notchQ) {
        if (!recordedBuffer) return;
        const ctx = getCtx();
        if (ctx.state === 'suspended') ctx.resume();
        if (playbackSource) { try { playbackSource.stop(); } catch (_) { } playbackSource = null; }

        analyser = ctx.createAnalyser();
        analyser.fftSize = 2048;
        analyserData = new Float32Array(analyser.frequencyBinCount);

        playbackSource = ctx.createBufferSource();
        playbackSource.buffer = recordedBuffer;

        if (filtered) {
            const freqs = (notchFreqs && notchFreqs.length) ? notchFreqs : [sinFrequency];
            const chain = _buildNotchChain(ctx, freqs, notchQ);
            playbackSource.connect(chain[0]);
            chain[chain.length - 1].connect(analyser);
        } else {
            playbackSource.connect(analyser);
        }
        analyser.connect(ctx.destination);
        playbackSource.start();

        playbackSource.onended = () => {
            if (dotnetRef) dotnetRef.invokeMethodAsync('OnPlaybackEnded');
        };
    }

    function getSpectrumData() {
        if (!analyser) return null;
        analyser.getFloatFrequencyData(analyserData);
        return Array.from(analyserData);
    }

    function getSampleRate() {
        return getCtx().sampleRate;
    }

    // ── Save as file ─────────────────────────────────────────────────────────

    function saveRecording(filename) {
        if (!recordedChunks.length) { console.warn('[Audio] Nothing recorded'); return; }
        const blob = new Blob(recordedChunks, { type: 'audio/webm' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = filename || 'recording.webm';
        a.click();
        setTimeout(() => URL.revokeObjectURL(url), 5000);
        console.log(`[Audio ${AUDIO_VERSION}] saved ${filename}`);
    }

    function hasRecording() {
        return !!recordedBuffer;
    }

    return {
        init,
        startSinWave, stopSinWave, setSinFrequency, setSinAmplitude,
        setMicGain,
        startRecording, stopRecording,
        playRecording, stopPlayback,
        playRecordingFiltered,
        playRecordingWithAnalyser, getSpectrumData, getSampleRate,
        saveRecording, hasRecording,
        getIsRecording: () => isRecording,
        getIsPlayingSin: () => isPlayingSin,
    };
})();
