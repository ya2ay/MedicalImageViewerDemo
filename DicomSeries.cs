using FellowOakDicom;
using FellowOakDicom.Imaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace MedicalRenderDemo
{
    public class DicomSeries
    {
        public List<DicomFile> Files { get; } = new();
        public int Rows => Files[0].Dataset.GetValue<int>(DicomTag.Rows, 0);
        public int Columns => Files[0].Dataset.GetValue<int>(DicomTag.Columns, 0);
        public int NumberOfFrames => Files.Count;

        public class VolumeData
        {
            public short[] Voxels; // 16-bit 像素
            public int Width, Height, Depth;
            public double SpacingX, SpacingY, SpacingZ;
        }
        public static DicomSeries LoadFromFolder(string folderPath)
        {
            var series = new DicomSeries();
            var files = Directory.GetFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly)
                                 .Where(f => IsDicomFile(f))
                                 .Select(f => DicomFile.Open(f))
                                 .ToList();

            if (files.Count == 0) throw new ArgumentException("No DICOM files found.");

            // 按 ImagePositionPatient 排序（Z 轴）
            files.Sort((a, b) =>
            {
                var posA = a.Dataset.GetValues<double>(DicomTag.ImagePositionPatient);
                var posB = b.Dataset.GetValues<double>(DicomTag.ImagePositionPatient);
                return posA[2].CompareTo(posB[2]); // Z 坐标
            });

            series.Files.AddRange(files);
            return series;
        }

        private static bool IsDicomFile(string path)
        {
            try
            {
                using var stream = File.OpenRead(path);
                var buffer = new byte[128 + 4];
                stream.Read(buffer, 0, buffer.Length);
                return Encoding.ASCII.GetString(buffer, 128, 4) == "DICM";
            }
            catch { return false; }
        }

        public VolumeData BuildVolume(DicomSeries series)
        {
            int w = series.Columns;
            int h = series.Rows;
            int d = series.NumberOfFrames;

            var voxels = new short[w * h * d];
            for (int i = 0; i < d; i++)
            {
                var pixelData = DicomPixelData.Create(series.Files[i].Dataset);
                var frame = pixelData.GetFrame(0);
                Buffer.BlockCopy(frame.Data, 0, voxels, i * w * h * sizeof(short), w * h * sizeof(short));
            }

            return new VolumeData { Voxels = voxels, Width = w, Height = h, Depth = d };
        }
    }
}

