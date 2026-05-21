using Microsoft.VisualStudio.TestTools.UnitTesting;
using MetadataExtractor;
using MetadataExtractor.Formats.QuickTime;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace TestProject1
{
    [TestClass]
    [TestCategory("Manual")]
    public class TestVideoRotation
    {
        // Change this path to test different video files
        private const string VideoFilePath = @"C:\Users\Calvi\OneDrive\Pictures\OldPictures\2025\20250719_131455.mp4";

        /// <summary>
        /// Reads rotation from video metadata using MetadataExtractor (same library as MyPix).
        /// Also parses the raw MP4 tkhd transformation matrix so we can verify the JS parser.
        /// Run from Test Explorer; output appears in the test detail pane.
        /// </summary>
        [TestMethod]
        public void Manual_ReadVideoRotation()
        {
            Assert.IsTrue(File.Exists(VideoFilePath), $"File not found: {VideoFilePath}");

            var sb = new StringBuilder();
            sb.AppendLine($"File: {VideoFilePath}");
            sb.AppendLine($"Size: {new FileInfo(VideoFilePath).Length:N0} bytes");
            sb.AppendLine();

            // ── 1. MetadataExtractor (same as MyPix desktop app) ──────────────────
            sb.AppendLine("=== MetadataExtractor ===");
            var dirs = ImageMetadataReader.ReadMetadata(VideoFilePath);
            foreach (var dir in dirs)
            {
                sb.AppendLine($"  [{dir.Name}]");
                foreach (var tag in dir.Tags)
                    sb.AppendLine($"    {tag.Name} = {tag.Description}");
            }

            double metaRotation = 0;
            foreach (var tkhd in dirs.OfType<QuickTimeTrackHeaderDirectory>())
            {
                if (tkhd.TryGetDouble(QuickTimeTrackHeaderDirectory.TagRotation, out var rot))
                {
                    sb.AppendLine($"\n>>> QuickTimeTrackHeaderDirectory.TagRotation = {rot}");
                    metaRotation = rot;
                    break;
                }
            }
            if (metaRotation == 0)
                sb.AppendLine("\n>>> No TagRotation found in QuickTimeTrackHeaderDirectory");

            // ── 2. Raw MP4 tkhd matrix parse (mirrors the JS getMp4RotationFromUrl) ─
            sb.AppendLine("\n=== Raw MP4 tkhd matrix parse ===");
            int rawRotation = ParseMp4TkhdRotation(VideoFilePath, sb);
            sb.AppendLine($"\n>>> Raw tkhd matrix rotation = {rawRotation}°");

            // ── 3. Summary ────────────────────────────────────────────────────────
            sb.AppendLine("\n=== Summary ===");
            sb.AppendLine($"  MetadataExtractor TagRotation : {metaRotation}");
            sb.AppendLine($"  Raw tkhd matrix rotation     : {rawRotation}°");
            sb.AppendLine($"  CSS transform needed         : rotate({rawRotation}deg)");

            Console.WriteLine(sb.ToString());

            // The test always passes — it's diagnostic output only.
            Assert.IsTrue(true);
        }

        /// <summary>
        /// Parses the MP4 tkhd box transformation matrix from raw bytes,
        /// exactly mirroring the getMp4RotationFromUrl JS function in PictureQuery.razor.
        /// Handles both front-loaded moov (normal) and end-of-file moov (large recordings).
        /// Logs every box it walks so we can see the exact file structure.
        /// </summary>
        private static int ParseMp4TkhdRotation(string filePath, StringBuilder sb)
        {
            var fi = new FileInfo(filePath);
            const int chunkSize = 128 * 1024; // 128 KB

            // Try the start of the file first (moov before mdat — common for streaming-optimised files)
            var startBuf = ReadFileChunk(filePath, 0, chunkSize);
            sb.AppendLine($"  Scanning first {startBuf.Length:N0} bytes...");
            int i = 0;
            int result = WalkBoxes(startBuf, ref i, startBuf.Length, 0, sb);
            if (result != 0) return result;

            // moov not found at start — scan the tail for the 'moov' signature then parse from there
            long tailStart = Math.Max(0, fi.Length - chunkSize);
            var tailBuf = ReadFileChunk(filePath, tailStart, chunkSize);
            sb.AppendLine($"\n  moov not in first chunk; scanning last {tailBuf.Length:N0} bytes (file offset {tailStart:N0})...");

            // Find 'moov' FourCC in the tail buffer
            int moovPos = FindFourCC(tailBuf, "moov");
            if (moovPos < 4)
            {
                sb.AppendLine("  'moov' FourCC not found in tail chunk.");
                return 0;
            }
            int moovBoxStart = moovPos - 4; // back up 4 bytes to the size field
            sb.AppendLine($"  Found 'moov' at tail buf offset {moovPos - 4} (file offset {tailStart + moovBoxStart:N0})");
            i = moovBoxStart;
            return WalkBoxes(tailBuf, ref i, tailBuf.Length, 0, sb);
        }

        private static byte[] ReadFileChunk(string path, long offset, int length)
        {
            using var fs = File.OpenRead(path);
            fs.Seek(offset, SeekOrigin.Begin);
            var actual = (int)Math.Min(length, fs.Length - offset);
            var buf = new byte[actual];
            fs.Read(buf, 0, actual);
            return buf;
        }

        private static int FindFourCC(byte[] buf, string fourcc)
        {
            byte b0 = (byte)fourcc[0], b1 = (byte)fourcc[1],
                 b2 = (byte)fourcc[2], b3 = (byte)fourcc[3];
            for (int i = 4; i < buf.Length - 3; i++)
                if (buf[i] == b0 && buf[i+1] == b1 && buf[i+2] == b2 && buf[i+3] == b3)
                    return i;
            return -1;
        }

        private static int WalkBoxes(byte[] buf, ref int i, int limit, int depth, StringBuilder sb)
        {
            while (i + 8 <= limit)
            {
                uint boxSize = ReadUInt32BE(buf, i);
                string boxType = ReadFourCC(buf, i + 4);
                string indent = new string(' ', depth * 2);

                if (boxSize < 8) break;

                sb.AppendLine($"{indent}[{boxType}] size={boxSize} at offset={i}");

                if (boxType == "tkhd")
                {
                    int deg = ParseTkhdMatrix(buf, i, sb, indent);
                    if (deg != 0) return deg;   // found a non-identity tkhd — use it
                    i += (int)boxSize;           // identity matrix — keep looking (may be audio track)
                    continue;
                }

                // Step into container boxes
                if (boxType is "moov" or "trak" or "mdia" or "minf" or "stbl")
                {
                    int inner = i + 8;
                    int innerLimit = (int)Math.Min((long)i + boxSize, limit);
                    int result = WalkBoxes(buf, ref inner, innerLimit, depth + 1, sb);
                    if (result != 0) return result;
                    i += (int)boxSize;
                    continue;
                }

                i += (int)boxSize;
            }
            return 0;
        }

        private static int ParseTkhdMatrix(byte[] buf, int boxOffset, StringBuilder sb, string indent)
        {
            // tkhd layout (version 0):
            //  4  box size
            //  4  box type "tkhd"
            //  1  version
            //  3  flags
            //  4  creation_time
            //  4  modification_time
            //  4  track_id
            //  4  reserved     ← easy to miss!
            //  4  duration
            //  8  reserved (2×int32)
            //  2  layer
            //  2  alternate_group
            //  2  volume
            //  2  reserved
            // 36  matrix  ← starts at offset +48 (v0) or +60 (v1)
            //
            // version 1: creation/modification/duration are 8 bytes each → +60

            byte version = buf[boxOffset + 8];
            int matrixOffset = boxOffset + (version == 1 ? 60 : 48);

            if (matrixOffset + 36 > buf.Length)
            {
                sb.AppendLine($"{indent}  tkhd matrix extends beyond read buffer");
                return 0;
            }

            // Matrix is stored as 9 × int32 big-endian fixed-point values
            // First 6 are 16.16 (divide by 65536), last 3 are 2.30 (divide by 1073741824)
            // Layout: [a][b][u] [c][d][v] [tx][ty][w]
            double a  = ReadInt32BE(buf, matrixOffset)      / 65536.0;
            double b  = ReadInt32BE(buf, matrixOffset + 4)  / 65536.0;
            // u at +8
            double c  = ReadInt32BE(buf, matrixOffset + 12) / 65536.0;
            double d  = ReadInt32BE(buf, matrixOffset + 16) / 65536.0;
            // v at +20, tx at +24, ty at +28, w at +32

            sb.AppendLine($"{indent}  tkhd version={version} matrixOffset={matrixOffset}");
            sb.AppendLine($"{indent}  matrix: a={a:F4} b={b:F4} c={c:F4} d={d:F4}");

            // Raw hex of the full 36-byte matrix for cross-checking the JS parser
            var hexMatrix = BitConverter.ToString(buf, matrixOffset, 36).Replace("-", " ");
            sb.AppendLine($"{indent}  matrix hex: {hexMatrix}");

            int ra = (int)Math.Round(a), rb = (int)Math.Round(b),
                rc = (int)Math.Round(c), rd = (int)Math.Round(d);

            // QuickTime tkhd matrix → CSS rotation degrees (clockwise)
            // MetadataExtractor reports TagRotation as negative CCW; CSS rotate() is CW.
            // Matrix patterns verified against QuickTime spec:
            //   CW 90  (TagRotation=-90): a=0  b=-1  c=1  d=0  → CSS 270 (or -90)
            //   CW 270 (TagRotation=-270/90): a=0  b=1  c=-1  d=0  → CSS 90
            int deg = (ra, rb, rc, rd) switch
            {
                ( 1,  0,  0,  1) => 0,
                ( 0,  1, -1,  0) => 90,
                (-1,  0,  0, -1) => 180,
                ( 0, -1,  1,  0) => 270,
                _ => -1   // unrecognised — log the raw values
            };

            if (deg == -1)
                sb.AppendLine($"{indent}  WARNING: unrecognised matrix pattern ra={ra} rb={rb} rc={rc} rd={rd}");

            return deg == -1 ? 0 : deg;
        }

        private static uint ReadUInt32BE(byte[] buf, int offset) =>
            ((uint)buf[offset] << 24) | ((uint)buf[offset + 1] << 16) |
            ((uint)buf[offset + 2] << 8) | buf[offset + 3];

        private static int ReadInt32BE(byte[] buf, int offset)
        {
            uint u = ReadUInt32BE(buf, offset);
            return (int)u;
        }

        private static string ReadFourCC(byte[] buf, int offset) =>
            new string(new[] {
                (char)buf[offset], (char)buf[offset+1],
                (char)buf[offset+2], (char)buf[offset+3]
            });
    }
}
