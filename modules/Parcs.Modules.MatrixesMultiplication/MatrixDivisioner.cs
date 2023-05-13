using Parcs.Modules.MatrixesMultiplication.Models;

namespace Parcs.Modules.MatrixesMultiplication
{
    public static class MatrixDivisioner
    {
        public static IEnumerable<Tuple<Matrix, Matrix>> Divide2(Matrix a, Matrix b)
        {
            yield return 
                Tuple.Create(a.SubMatrix(0, 0, b.Height / 2, b.Width), b);
            yield return
                Tuple.Create(a.SubMatrix(0, 0, a.Height / 2 + a.Height % 2, a.Width), b);
        }

        public static IEnumerable<Tuple<Matrix, Matrix>> Divide4(Matrix a, Matrix b)
        {
            yield return
                Tuple.Create(a.SubMatrix(0, 0, a.Height / 2, a.Width), b.SubMatrix(0, 0, b.Height, b.Width / 2));
            yield return
                Tuple.Create(a.SubMatrix(0, 0, a.Height / 2, a.Width), b.SubMatrix(0, b.Width / 2, b.Height, b.Width / 2 + b.Width % 2));
            yield return
                Tuple.Create(a.SubMatrix(a.Height / 2, 0, a.Height / 2 + a.Height % 2, b.Width), b.SubMatrix(0, 0, b.Height, b.Width / 2));
            yield return
                Tuple.Create(a.SubMatrix(a.Height / 2, 0, a.Height / 2 + a.Height % 2, b.Width), b.SubMatrix(0, b.Width / 2, b.Height, b.Width / 2 + b.Width % 2));
        }

        public static IEnumerable<Tuple<Matrix, Matrix>> Divide8(Matrix a, Matrix b)
        {
            yield return
                Tuple.Create(a.SubMatrix(0, 0, a.Width / 2, a.Width / 2), b.SubMatrix(0, 0, b.Width / 2, b.Width / 2));
            yield return
                Tuple.Create(a.SubMatrix(0, a.Width / 2, a.Width / 2, a.Width / 2), b.SubMatrix(b.Width / 2, 0, b.Width / 2, b.Width / 2));
            yield return
                Tuple.Create(a.SubMatrix(0, 0, a.Width / 2, a.Width / 2), b.SubMatrix(0, b.Width / 2, b.Width / 2, b.Width / 2));
            yield return
                Tuple.Create(a.SubMatrix(0, a.Width / 2, a.Width / 2, a.Width / 2), b.SubMatrix(b.Width / 2, b.Width / 2, b.Width / 2, b.Width / 2));
            yield return
                Tuple.Create(a.SubMatrix(a.Width / 2, 0, a.Width / 2, a.Width / 2), b.SubMatrix(0, 0, b.Width / 2, b.Width / 2));
            yield return
                Tuple.Create(a.SubMatrix(a.Width / 2, a.Width / 2, a.Width / 2, a.Width / 2), b.SubMatrix(b.Width / 2, 0, b.Width / 2, b.Width / 2));
            yield return
                Tuple.Create(a.SubMatrix(a.Width / 2, 0, a.Width / 2, a.Width / 2), b.SubMatrix(0, b.Width / 2, b.Width / 2, b.Width / 2));
            yield return
                Tuple.Create(a.SubMatrix(a.Width / 2, a.Width / 2, a.Width / 2, a.Width / 2), b.SubMatrix(b.Width / 2, b.Width / 2, b.Width / 2, b.Width / 2));
        }

        public static Matrix Join2(Matrix resultMatrix, IList<Matrix> matrixes)
        {
            resultMatrix.FillSubMatrix(matrixes[0], 0, 0);
            resultMatrix.FillSubMatrix(matrixes[1], (resultMatrix.Height / 2), 0);

            return resultMatrix;
        }

        public static Matrix Join4(Matrix resultMatrix, IList<Matrix> matrixes)
        {
            resultMatrix.FillSubMatrix(matrixes[0], 0, 0);
            resultMatrix.FillSubMatrix(matrixes[1], 0, resultMatrix.Width / 2);

            resultMatrix.FillSubMatrix(matrixes[2], resultMatrix.Height / 2, 0);
            resultMatrix.FillSubMatrix(matrixes[3], resultMatrix.Height / 2, resultMatrix.Width / 2);

            return resultMatrix;
        }
            
        public static Matrix Join8(Matrix resultMatrix, IList<Matrix> matrixes)
        {
            var parts = new Matrix[2, 2];

            parts[0, 0] = matrixes[0];
            parts[0, 0].Add(matrixes[1]);
            resultMatrix.SetSubmatrix(parts[0, 0], 0, 0);

            parts[0, 1] = matrixes[2];
            parts[0, 1].Add(matrixes[3]);
            resultMatrix.SetSubmatrix(parts[0, 1], 0, resultMatrix.Width / 2);

            parts[1, 0] = matrixes[4];
            parts[1, 0].Add(matrixes[5]);
            resultMatrix.SetSubmatrix(parts[1, 0], resultMatrix.Height / 2, 0);

            parts[1, 1] = matrixes[6];
            parts[1, 1].Add(matrixes[7]);
            resultMatrix.SetSubmatrix(parts[1, 1], resultMatrix.Height / 2, resultMatrix.Width / 2);

            return resultMatrix;
        }
    }
}