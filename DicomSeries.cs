using FellowOakDicom;
using FellowOakDicom.Imaging;
using System;
using System.Collections.Generic;
using System.Data;
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
        public double SliceThickness => Files[0].Dataset.GetValue<double>(DicomTag.SliceThickness, 0);
        public double PixelSpacingX => Files[0].Dataset.GetValues<double>(DicomTag.PixelSpacing)[1];
        public double PixelSpacingY => Files[0].Dataset.GetValues<double>(DicomTag.PixelSpacing)[0];
        public int WindowWidth => Files[0].Dataset.GetSingleValue<int>(DicomTag.WindowWidth);
        public int WindowCenter => Files[0].Dataset.GetSingleValue<int>(DicomTag.WindowCenter);

    public class VolumeData
        {
            public ushort[] Voxels; // 16-bit 像素
            public int Width, Height, Depth;
            public double SpacingX, SpacingY, SpacingZ;
        }

        // DicomSeries.cs —— 替换 BuildVolume 方法
        public VolumeData BuildVolume()
        {
            if (Files.Count == 0) throw new InvalidOperationException("No DICOM files loaded.");

            int w = Columns;
            int h = Rows;
            int d = NumberOfFrames;

            var voxels = new ushort[w * h * d];

            for (int i = 0; i < d; i++)
            {
                var dataset = Files[i].Dataset;
                var pixelData = DicomPixelData.Create(dataset);
                var frame = pixelData.GetFrame(0);

                // 获取原始字节
                byte[] rawBytes = frame.Data;

                bool isSigned = dataset.GetValue<ushort>(DicomTag.PixelRepresentation, 0) == 1;
                if (!dataset.TryGetValue<int>(DicomTag.BitsAllocated, 0, out int bitsAllocated))
                {
                    bitsAllocated = 16; // 默认值
                }
                //int bitsAllocated = dataset.GetValue<int>(DicomTag.BitsAllocated, 16);

                if (bitsAllocated == 16)
                {
                    for (int j = 0; j < w * h; j++)
                    {
                        ushort value = BitConverter.ToUInt16(rawBytes, j * 2);
                        voxels[i * w * h + j] = isSigned ? unchecked((ushort)value) : (ushort)value;
                    }
                }
                else if (bitsAllocated == 8)
                {
                    for (int j = 0; j < w * h; j++)
                    {
                        voxels[i * w * h + j] = (ushort)rawBytes[j];
                    }
                }
                else
                {
                    throw new NotSupportedException($"BitsAllocated={bitsAllocated} not supported.");
                }
            }

            return new VolumeData
            {
                Voxels = voxels,
                Width = w,
                Height = h,
                Depth = d,
                SpacingX = PixelSpacingX,
                SpacingY = PixelSpacingY,
                SpacingZ = SliceThickness
            };
        }
    }
}

