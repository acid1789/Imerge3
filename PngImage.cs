using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;

namespace IMerge3
{
    class PngImage
    {
        const long PNG = 0x0a1a0a0d474e5089;
        const int IHDR = 0x49484452;
        const int PLTE = 0x504c5445;
        const int IDAT = 0x49444154;
        const int IEND = 0x49454e44;

        const int tEXt = 0x74455874;
        const int iTXt = 0x69545874;
        const int tRNS = 0x74524e53;
        const int pHYs = 0x70485973;


        class Chunk
        {
            public int ChunkType;

            protected Chunk(int type)
            {
                ChunkType = type;
            }

            public static Chunk Create(int length, int type, byte[] Data, int crc)
            {
                switch (type)
                {
                    case IHDR: return new IHDRChunk(Data);
                    case IDAT: return new IDATChunk(Data);
                    default:
                        return new GenericChunk(type) { Length = length, Data = Data, Crc = crc };
                }
            }
        }

        class GenericChunk : Chunk
        {
            public int Length;
            public byte[] Data;
            public int Crc;

            public GenericChunk(int type) : base(type) { }
        }

        class IHDRChunk : Chunk
        {
            public int Width;
            public int Height;
            public int BitDepth;
            public int ColorType;
            public int CompressionMethod;
            public int FilterMethod;
            public int InterlaceMethod;

            public IHDRChunk(byte[] data) : base(IHDR)
            {
                BinaryReader br = new BinaryReader(new MemoryStream(data));
                Width = Program.ByteSwap(br.ReadInt32());
                Height = Program.ByteSwap(br.ReadInt32());
                BitDepth = br.ReadByte();
                ColorType = br.ReadByte();
                CompressionMethod = br.ReadByte();
                FilterMethod = br.ReadByte();
                InterlaceMethod = br.ReadByte();
            }

            public int PixelSize
            {
                get
                {
                    switch (ColorType)
                    {
                        case 2: return (3 * BitDepth) / 8;
                        default:
                            return -1;
                    }
                }
            }                    
        }

        class IDATChunk : Chunk
        {
            public byte[] RawData;

            public IDATChunk(byte[] data) : base(IDAT)
            {
                RawData = data;
            }            
        }



        #region PngImage
        List<Chunk> _chunks;
        IHDRChunk _hdrChunk;

        byte[] _decompressedRows;

        PngImage()
        {
            _chunks = new List<Chunk>();
        }

        public int Width { get { return _hdrChunk.Width; } }
        public int Height { get { return _hdrChunk.Height; } }

        void Decompress()
        {
            MemoryStream ms = new MemoryStream();
            foreach (Chunk ck in _chunks)
            {
                if (ck.ChunkType == IDAT)
                {
                    IDATChunk idc = (IDATChunk)ck;
                    ms.Write(idc.RawData, 0, idc.RawData.Length);
                }
            }
            byte[] rawComp = ms.ToArray();
            byte[] c = new byte[rawComp.Length - 6];
            Buffer.BlockCopy(rawComp, 2, c, 0, c.Length);
            DeflateStream ds = new DeflateStream(new MemoryStream(c), CompressionMode.Decompress);

            MemoryStream decompressed = new MemoryStream();
            ds.CopyTo(decompressed);
            _decompressedRows = decompressed.ToArray();            
        }

        public PixelData GetRow(int rowIndex)
        {
            int rowSize = (_hdrChunk.PixelSize * _hdrChunk.Width) + 1;

            int rowOffset = (rowIndex * rowSize);
            PixelData row = new PixelData();
            for (int i = 1; i < rowSize; i += 3)
            {
                row.AddColor(_decompressedRows[rowOffset + i + 0],
                                _decompressedRows[rowOffset + i + 1],
                                _decompressedRows[rowOffset + i + 2]);
            }
            return row;
        }
        #endregion

        public static PngImage Load(string pngFile)
        {
            BinaryReader br = new BinaryReader(File.OpenRead(pngFile));
            long pngId = br.ReadInt64();
            if (pngId != PNG)
                throw new InvalidDataException("file is not a png file");

            // Chunks
            PngImage image = new PngImage();
            while (br.BaseStream.Position < br.BaseStream.Length)
            {
                int length = Program.ByteSwap(br.ReadInt32());
                int type = Program.ByteSwap(br.ReadInt32());
                byte[] data = br.ReadBytes(length);
                int crc = Program.ByteSwap(br.ReadInt32());

                byte[] test = new byte[data.Length + 4];
                test[3] = (byte)(type & 0xFF);
                test[2] = (byte)((type >> 8) & 0xFF);
                test[1] = (byte)((type >> 16) & 0xFF);
                test[0] = (byte)((type >> 24) & 0xFF);
                Buffer.BlockCopy(data, 0, test, 4, data.Length);
                uint check = Crc32.Compute(test);

                Chunk chunk = Chunk.Create(length, type, data, crc);
                if (type == IHDR)
                    image._hdrChunk = (IHDRChunk)chunk;
                image._chunks.Add(chunk);
            }

            // Decompress all pixels
            image.Decompress();

            br.Close();
            return image;
        }

        public static void WriteFile(string pngFile, PixelData[] rows, PngImage reference)
        {
            // Build binary row data
            byte[] rowData = BuildRows(rows);

            FileStream fs = File.Create(pngFile);
            BinaryWriter bw = new BinaryWriter(fs);

            // Write PNG header
            bw.Write(PNG);

            // Write IHDR
            byte[] ihdr = GenerateIHDR(reference, rows[0].Width);
            WriteChunk(bw, ihdr);

            int dataChunkCount = rowData.Length / 8192;
            for (int i = 0; i < dataChunkCount; i++)
            {
                byte[] chunkData = GenerateIDAT(rowData, i * 8192, 8192);
                WriteChunk(bw, chunkData);
            }
            if (rowData.Length % 8192 != 0)
            {
                // Write remaining bytes
                int start = dataChunkCount * 8192;
                byte[] chunkData = GenerateIDAT(rowData, start, rowData.Length - start);
                WriteChunk(bw, chunkData);
            }

            // Write IEND
            byte[] iend = GenerateIEND();
            WriteChunk(bw, iend);


            bw.Close();
        }

        static void WriteChunk(BinaryWriter bw, byte[] chunkData)
        {
            uint crc = Crc32.Compute(chunkData);

            bw.Write(Program.ByteSwap(chunkData.Length - 4));
            bw.Write(chunkData);
            bw.Write(Program.ByteSwap((int)crc));
        }

        static byte[] GenerateIEND()
        {
            MemoryStream ms = new MemoryStream();
            BinaryWriter bw = new BinaryWriter(ms);

            bw.Write(Program.ByteSwap(IEND));
            return ms.ToArray();
        }

        static byte[] GenerateIHDR(PngImage reference, int width)
        {
            MemoryStream ms = new MemoryStream();
            BinaryWriter bw = new BinaryWriter(ms);

            bw.Write(Program.ByteSwap(IHDR));
            bw.Write(Program.ByteSwap(width));
            bw.Write(Program.ByteSwap(reference.Height));
            bw.Write((byte)reference._hdrChunk.BitDepth);
            bw.Write((byte)reference._hdrChunk.ColorType);
            bw.Write((byte)reference._hdrChunk.CompressionMethod);
            bw.Write((byte)reference._hdrChunk.FilterMethod);
            bw.Write((byte)reference._hdrChunk.InterlaceMethod);

            byte[] d = ms.ToArray();
            bw.Close();
            return d;
        }

        static byte[] GenerateIDAT(byte[] rowData, int index, int length)
        {
            MemoryStream ms = new MemoryStream();
            BinaryWriter bw = new BinaryWriter(ms);

            bw.Write(Program.ByteSwap(IDAT));
            bw.Write(rowData, index, length);

            return ms.ToArray();
        }

        static byte[] BuildRows(PixelData[] rows)
        {
            MemoryStream ms = new MemoryStream();
            BinaryWriter bw = new BinaryWriter(ms);

            foreach (PixelData row in rows)
            {
                bw.Write((byte)0);  // Filter
                row.Write(bw);
            }

            uint a32 = Crc32.Adler32(ms.ToArray());

            ms.Seek(0, SeekOrigin.Begin);
            byte[] compressed = Compress(ms);

            bw.Close();
            ms = new MemoryStream();
            bw = new BinaryWriter(ms);
            bw.Write((byte)0x78);
            bw.Write((byte)0x5e);
            bw.Write(compressed);
            bw.Write(Program.ByteSwap((int)a32));


            return ms.ToArray();
        }

        static byte[] Compress(Stream input)
        {
            using (var compressStream = new MemoryStream())
            using (var compressor = new DeflateStream(compressStream, CompressionMode.Compress))
            {
                input.CopyTo(compressor);
                compressor.Close();
                return compressStream.ToArray();
            }
        }
    }
}
