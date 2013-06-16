using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace vpinsim
{
    /// <summary>
    /// the observed block. there would be a piece of message for
    /// this block to be maintained by all the vehicles in this block
    /// </summary>
    public class Block
    {
        public double Xmin;
        public double Xmax;
        public double Ymin;
        public double Ymax;


        /// <summary>
        /// Constructor from max and min axis of x and y. In degree.
        /// </summary>
        /// <param name="xmin"></param>
        /// <param name="xmax"></param>
        /// <param name="ymin"></param>
        /// <param name="ymax"></param>
        public Block(double xmin, double xmax, double ymin, double ymax)
        {
            this.Xmin = xmin;
            this.Xmax = xmax;
            this.Ymin = ymin;
            this.Ymax = ymax;
        }

        /// <summary>
        /// Construct the block from two diagnal points. In degree.
        /// </summary>
        /// <param name="A"></param>
        /// <param name="B"></param>
        public Block(Point A, Point B)
        {
            this.Xmin = Math.Min(A.X, B.X);
            this.Xmax = Math.Max(A.X, B.X);
            this.Ymin = Math.Min(A.Y, B.Y);
            this.Ymax = Math.Max(A.Y, B.Y);
        }

        /// <summary>
        /// Construct the block by the center and the edge length. 
        /// In degree.
        /// </summary>
        /// <param name="center"></param>
        /// <param name="length"></param>
        public Block(Point center, double length)
        {
            this.Xmin = center.X - length / 2;
            this.Xmax = center.X + length / 2;
            this.Ymin = center.Y - length / 2;
            this.Ymax = center.Y + length / 2;
        }

        public bool ContainsPoint(Point p)
        {
            return Calculator.IsPointInRectangle(p, new Point(Xmin, Ymin),
                new Point(Xmax, Ymax));
        }
    }
}
