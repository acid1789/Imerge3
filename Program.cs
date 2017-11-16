using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace IMerge3
{
    class Program
    {
        public static int ByteSwap(int i)
        {
            return ((i & 0x000000FF) << 24) | ((i & 0x0000FF00) << 8) | ((i & 0x00FF0000) >> 8) | (int)((i & 0xFF000000) >> 24);
        }

        static void Main(string[] args)
        {
            //PngImage.Load("pngsOut/100001.png");

            if (args.Length < 3)
            {
                Console.WriteLine("Usage: {0} <input_folder> <output_folder> <input_count (3 or 4)>", Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetEntryAssembly().Location));
                return;
            }

            if (!Directory.Exists(args[0]))
            {
                Console.WriteLine("Error - Input folder does not exist: " + args[1]);
                return;
            }

            int inputCount = 0;
            if (!int.TryParse(args[2], out inputCount) || (inputCount != 3 && inputCount != 4) )
            {
                Console.WriteLine("Invalid inputCount (must be 3 or 4): " + args[2]);
                return;
            }

            // Make sure output directory exists
            Directory.CreateDirectory(args[1]);

            string[] inputFiles = Directory.GetFiles(args[0], "*.png", SearchOption.TopDirectoryOnly);
            List<string> ifiles = new List<string>(inputFiles);
            ifiles.Sort();
            
            int outCounter = 100001;
            int count = ifiles.Count / inputCount;
            for (int i = 0; i < count; i++)
            {
                int index = i * inputCount;
                string outputFile = args[1] + Path.DirectorySeparatorChar + outCounter + ".png";
                List<string> mifiles = new List<string>();
                for (int j = 0; j < inputCount; j++)
                    mifiles.Add(ifiles[index + j]);
                MergeImages(mifiles.ToArray(), outputFile);
                outCounter++;
            }
        }

        static void MergeImages(string[] inputs, string output)
        {
            Console.Write("Merging: ");
            for (int i = 0; i < inputs.Length; i++)
                Console.Write("{0} {1} ", inputs[i], i == inputs.Length - 1 ? "->" : "+");
            Console.WriteLine(output);

            // Load the input images
            List<PngImage> images = new List<PngImage>();
            foreach (string input in inputs)
            {
                try
                {
                    images.Add(PngImage.Load(input));                    
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed to open: input");
                    return;
                }
            }

            // Make sure we can merge

            // Merge into one image
            List<PixelData> outputRows = new List<PixelData>();
            for (int y = 0; y < images[0].Height; y++)
            {
                PixelData outputRow = new PixelData();
                foreach (PngImage image in images)
                {
                    PixelData pd = image.GetRow(y);
                    outputRow.Add(pd);
                }
                outputRows.Add(outputRow);
            }

            // Save the output
            PngImage.WriteFile(output, outputRows.ToArray(), images[0]);
        }
    }
}
