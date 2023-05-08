using System.Text;
using System.Threading;

namespace Parcs.Modules.MatrixesMultiplication.Models
{
    public class Matrix
    {
        private const int MaxRandomValue = 100;

        public Matrix()
        {
        }

        public Matrix(int heigth, int width, bool randomFill = false)
        {
            Height = heigth;
            Width = width;

            Data = new List<List<int>>(Height);

            for (int i = 0; i < Height; ++i)
            {
                var row = new List<int>(Width);

                for (int j = 0; j < Width; ++j)
                {
                    row.Add(0);
                }

                Data.Add(row);
            }

            if (randomFill)
            {
                RandomFill();
            }
        }

        public int Height { get; set; }

        public int Width { get; set; }

        public List<List<int>> Data { get; set; }

        public Matrix SubMatrix(int top, int left, int height, int width)
        {
            Matrix subMatrix = null;

            if ((top >= 0) && (left >= 0) && (top + height <= Height) && (left + width <= Width))
            {
                subMatrix = new Matrix(height, width);

                for (int i = 0; i < height; i++)
                {
                    for (int j = 0; j < width; j++)
                    {
                        subMatrix[i, j] = Data[top + i][left + j];
                    }
                }
            }

            return subMatrix;
        }

        public int this[int x, int y]
        {
            get
            {
                return Data[x][y];
            }

            set
            {
                Data[x][y] = value;
            }
        }

        public Matrix Add(Matrix matrix)
        {
            if (matrix.Width != Width || matrix.Height != Height)
            {
                Console.WriteLine("Different dimentions");
                return null;
            }

            for (int i = 0; i < Height; ++i)
            {
                for (int j = 0; j < Width; ++j)
                {
                    this[i, j] += matrix[i, j];
                }
            }

            return this;
        }

        public Matrix MultiplyBy(Matrix matrix, CancellationToken token = default)
        {
            Matrix resultMatrix = null;

            if (Width != matrix.Height)
            {
                Console.WriteLine("Cannot multiply matrixes with such dimentions");
            }

            else
            {
                resultMatrix = new Matrix(Height, matrix.Width);

                for (int i = 0; i < Height; i++)
                {
                    token.ThrowIfCancellationRequested();
                    for (int j = 0; j < matrix.Width; j++)
                    {
                        resultMatrix[i, j] = 0;
                        for (int pos = 0; pos < Width; pos++)
                        {
                            resultMatrix[i, j] += this[i, pos] * matrix[pos, j];
                        }
                    }
                }
            }

            return resultMatrix;
        }

        public void Assign(Matrix matrix)
        {
            Height = matrix.Height;
            Width = matrix.Width;

            Data = new List<List<int>>(Height);

            for (int i = 0; i < Height; ++i)
            {
                var row = new List<int>(Width);

                for (int j = 0; j < Width; ++j)
                {
                    row.Add(0);
                }

                Data.Add(row);
            }

            for (int i = 0; i < Height; ++i)
            {
                for (int j = 0; j < Width; ++j)
                {
                    this[i, j] = matrix[i, j];
                }
            }
        }

        public void FillSubMatrix(Matrix source, int top, int left)
        {
            if (top + source.Height <= Height && left + source.Width <= Width)
            {
                for (int i = 0; i < source.Height; i++)
                {
                    for (int j = 0; j < source.Width; j++)
                    {
                        Data[top + i][left + j] = source[i, j];
                    }
                }
            }
        }

        public void RandomFill()
        {
            var rand = new Random();
            for (int i = 0; i < Height; ++i)
            {
                for (int j = 0; j < Width; ++j)
                {
                    Data[i][j] = rand.Next(MaxRandomValue);
                }
            }
        }

        public static Matrix LoadFromStream(Stream stream)
        {
            using var reader = new BinaryReader(stream);

            var m = reader.ReadInt32();
            var n = reader.ReadInt32();

            var matrix = new Matrix(m, n);

            for (var i = 0; i < m; i++)
            {
                for (var j = 0; j < n; j++)
                {
                    matrix[i, j] = reader.ReadInt32();
                }
            }

            return matrix;
        }

        public Task WriteToStreamAsync(FileStream fileStream, CancellationToken cancellationToken = default)
        {
            using var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(ToString()));
            return memoryStream.CopyToAsync(fileStream, cancellationToken);
        }

        public override string ToString()
        {
            var stringBuilder = new StringBuilder();

            for (var i = 0; i < Width; i++)
            {
                for (var j = 0; j < Height; j++)
                {
                    stringBuilder.Append(this[i, j]);

                    if (j != Height - 1)
                    {
                        stringBuilder.Append(' ');
                    }
                }

                stringBuilder.AppendLine();
            }

            return stringBuilder.ToString();
        }
    }
}