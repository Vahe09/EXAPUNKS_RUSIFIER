param(
    [Parameter(Mandatory = $true)]
    [string]$TexPath,

    [Parameter(Mandatory = $true)]
    [string]$OutputPng
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing

$source = @"
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

public static class ExapunksTexDecoder
{
    private static byte[] DecodeLz4(byte[] input, int expectedLength)
    {
        byte[] output = new byte[expectedLength];
        int src = 0;
        int dst = 0;

        while (src < input.Length)
        {
            int token = input[src++];
            int literalLength = token >> 4;
            if (literalLength == 15)
            {
                byte ext;
                do
                {
                    if (src >= input.Length)
                    {
                        throw new InvalidDataException("Unexpected end of LZ4 literal length.");
                    }
                    ext = input[src++];
                    literalLength += ext;
                }
                while (ext == 255);
            }

            if (src + literalLength > input.Length || dst + literalLength > output.Length)
            {
                throw new InvalidDataException("Invalid LZ4 literal range.");
            }

            Buffer.BlockCopy(input, src, output, dst, literalLength);
            src += literalLength;
            dst += literalLength;

            if (src >= input.Length)
            {
                break;
            }

            if (src + 2 > input.Length)
            {
                throw new InvalidDataException("Unexpected end of LZ4 offset.");
            }

            int offset = input[src] | (input[src + 1] << 8);
            src += 2;
            if (offset <= 0 || offset > dst)
            {
                throw new InvalidDataException("Invalid LZ4 offset.");
            }

            int matchLength = token & 0x0F;
            if (matchLength == 15)
            {
                byte ext;
                do
                {
                    if (src >= input.Length)
                    {
                        throw new InvalidDataException("Unexpected end of LZ4 match length.");
                    }
                    ext = input[src++];
                    matchLength += ext;
                }
                while (ext == 255);
            }
            matchLength += 4;

            if (dst + matchLength > output.Length)
            {
                throw new InvalidDataException("Invalid LZ4 match range.");
            }

            int matchSrc = dst - offset;
            for (int i = 0; i < matchLength; i++)
            {
                output[dst++] = output[matchSrc + i];
            }
        }

        if (dst != output.Length)
        {
            throw new InvalidDataException("Decoded length mismatch.");
        }

        return output;
    }

    public static void DecodeToPng(string texPath, string outputPng)
    {
        byte[] bytes = File.ReadAllBytes(texPath);
        if (bytes.Length < 60)
        {
            throw new InvalidDataException("TEX file is too short.");
        }

        int width = BitConverter.ToInt32(bytes, 4);
        int height = BitConverter.ToInt32(bytes, 8);
        int compressedLength = BitConverter.ToInt32(bytes, 56);
        if (width <= 0 || height <= 0 || compressedLength <= 0 || bytes.Length < 60 + compressedLength)
        {
            throw new InvalidDataException("Invalid TEX header.");
        }

        byte[] compressed = new byte[compressedLength];
        Buffer.BlockCopy(bytes, 60, compressed, 0, compressedLength);
        byte[] rgbaBottomUp = DecodeLz4(compressed, width * height * 4);

        using (Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb))
        {
            Rectangle rect = new Rectangle(0, 0, width, height);
            BitmapData data = bitmap.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                int stride = Math.Abs(data.Stride);
                byte[] bgraTopDown = new byte[stride * height];

                for (int y = 0; y < height; y++)
                {
                    int sourceY = height - 1 - y;
                    for (int x = 0; x < width; x++)
                    {
                        int srcIndex = ((sourceY * width) + x) * 4;
                        int dstIndex = (y * stride) + (x * 4);
                        bgraTopDown[dstIndex + 0] = rgbaBottomUp[srcIndex + 2];
                        bgraTopDown[dstIndex + 1] = rgbaBottomUp[srcIndex + 1];
                        bgraTopDown[dstIndex + 2] = rgbaBottomUp[srcIndex + 0];
                        bgraTopDown[dstIndex + 3] = rgbaBottomUp[srcIndex + 3];
                    }
                }

                Marshal.Copy(bgraTopDown, 0, data.Scan0, bgraTopDown.Length);
            }
            finally
            {
                bitmap.UnlockBits(data);
            }

            string outDir = Path.GetDirectoryName(outputPng);
            if (!string.IsNullOrEmpty(outDir))
            {
                Directory.CreateDirectory(outDir);
            }
            bitmap.Save(outputPng, ImageFormat.Png);
        }
    }
}
"@

Add-Type -TypeDefinition $source -Language CSharp -ReferencedAssemblies @("System.dll", "System.Drawing.dll")
[ExapunksTexDecoder]::DecodeToPng($TexPath, $OutputPng)
