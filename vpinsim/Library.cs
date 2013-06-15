using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.IO;
using System.Data.OleDb;

namespace vpinsim
{
    /// <summary>
    /// 文件处理函数。
    /// </summary>
    public class Processor
    {
        /// <summary>
        /// 地球周长（千米）。
        /// </summary>
        public static double EARTH_RADIUS = 6371.012;

        /// <summary>
        /// 上海东西宽（千米）。
        /// </summary>
        public static double SH_WIDTH = 100;

        /// <summary>
        /// 上海南北长（千米）。
        /// </summary>
        public static double SH_HEIGHT = 120;

        ///// <summary>
        ///// 分区域索引网格列数。
        ///// </summary>
        //public const int XGridNum = 10;

        ///// <summary>
        ///// 分区域索引网格行数。
        ///// </summary>
        //public const int YGridNum = 12;

        /// <summary>
        /// 得到 .shp 地图文件对应的 .grd 索引文件。
        /// </summary>
        /// <param name="shpFileName">要生成索引文件的地图文件名（*.shp）。</param>
        /// <param name="grdFileName">所生成的索引文件名（*.grd）。</param>
        public static void ShpToGrd(string shpFileName, string grdFileName)
        {
            if (!File.Exists(shpFileName))
            {
                throw new FileNotFoundException();
            }

            #region Initialization.
            MainFile mf = new MainFile(shpFileName);
            double Xmin = mf.header.Xmin;
            double Xmax = mf.header.Xmax;
            double Ymin = mf.header.Ymin;
            double Ymax = mf.header.Ymax;
            const int XGridNum = 500; // X 方向上网格的个数。
            const int YGridNum = 600; // Y 方向上网格的个数。
            double dx = (Xmax - Xmin) / XGridNum;
            double dy = (Ymax - Ymin) / YGridNum;
            List<int>[,] Grids = new List<int>[XGridNum, YGridNum];
            for (int i = 0; i < XGridNum; i++)
            {
                for (int j = 0; j < YGridNum; j++)
                {
                    Grids[i, j] = new List<int>();
                }
            }
            #endregion

            #region Calculate whether it is out of a grid for every segment in every polyline.
            // Go through all roads on the map.
            for (int i = 0; i < mf.records.Count; i++)
            {
                PolyLine polyline = (mf.records[i].content as PolyLineRecordContent).PolyLine;
                int xa = (int)((polyline.Box[0] - Xmin) / dx);
                int ya = (int)((polyline.Box[1] - Ymin) / dy);
                int xb = (int)((polyline.Box[2] - Xmin) / dx);
                int yb = (int)((polyline.Box[3] - Ymin) / dy);
                xb = (xb == XGridNum) ? xb - 1 : xb;
                yb = (yb == YGridNum) ? yb - 1 : yb;
                for (int j = 0; j < polyline.NumParts; j++)
                {
                    int begin = polyline.Parts[j];
                    int end = (j == polyline.NumParts - 1) ? (polyline.NumPoints - 1) : (polyline.Parts[j + 1] - 1);
                    for (int k = begin; k < end; k++)
                    {
                        Point p1 = polyline.Points[k];
                        Point p2 = polyline.Points[k + 1];

                        for (int x = xa; x <= xb; x++)
                        {
                            for (int y = ya; y <= yb; y++)
                            {
                                double left = Xmin + x * dx;
                                double right = Xmin + (x + 1) * dx;
                                double bottom = Ymin + y * dy;
                                double top = Ymin + (y + 1) * dy;
                                if (!Calculator.IsSegmentOutOfRectangle(p1, p2, left, right, top, bottom))
                                {
                                    if (!Grids[x, y].Contains(i))
                                    {
                                        Grids[x, y].Add(i);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            // Go through all grids on the map.
            //for (int x = 0; x < XGridNum; x++)
            //{
            //    for (int y = 0; y < YGridNum; y++)
            //    {
            //        // 如果某个网格中包含零条路，则把距离它（中心）最近的一条路加进去。
            //        if (Grids[x, y].Count == 0)
            //        {
            //            Point p = new Point(Xmin + (x + 0.5) * dx, Ymin + (y + 0.5) * dy);
            //            int index = Calculator.IndexOfNearestPolyLine(p, mf);
            //            Grids[x, y].Add(index);
            //        }
            //    }
            //}
            #endregion

            #region Write to file.
            try
            {
                FileStream fs = new FileStream(grdFileName, FileMode.Create);
                BinaryWriter bw = new BinaryWriter(fs);

                // 写文件头。
                bw.Write(XGridNum);
                bw.Write(YGridNum);

                // 写文件记录。
                for (int i = 0; i < XGridNum; i++)
                {
                    for (int j = 0; j < YGridNum; j++)
                    {
                        bw.Write(Grids[i, j].Count);
                        for (int k = 0; k < Grids[i, j].Count; k++)
                        {
                            bw.Write(Grids[i, j][k]);
                        }
                    }
                }

                bw.Close();
                fs.Close();
            }
            catch (Exception ex)
            {
                //MessageBox.Show(ex.Message);
                Console.WriteLine("Error! " + ex.Message);
            }
            #endregion
        }

        /// <summary>
        /// 得到 .shp 地图文件对应的 .tpl 拓扑结构文件。
        /// </summary>
        /// <param name="shpFileName">要生成拓扑结构文件的地图文件名（*.shp）。</param>
        /// <param name="tplFileName">所生成的拓扑结构文件名（*.tpl）。</param>
        public static void ShpToTpl(string shpFileName, string tplFileName)
        {
            if (!File.Exists(shpFileName))
            {
                throw new FileNotFoundException();
            }

            #region Initialization.
            MainFile mf = new MainFile(shpFileName);

            // 各个数据结构。
            // 记录每条 Edge 所对应的 PolyLine 的索引号。（一条 Edge 对应一条 PolyLine）
            // int数组含义：[0]代表 PolyLine 的索引号，[1]代表起点 Point 的索引号，[2]代表终点 Point 的索引号。
            List<int[]> EdgeToPolyLine = new List<int[]>();
            // 记录每条 PolyLine 所对应的 Edge 的索引号。（一条 PolyLine 对应多条 Edge）
            int[][] PolyLineToEdge = new int[mf.records.Count][];
            // 记录每条 Edge 的信息。
            List<Edge> Edges = new List<Edge>();
            // 记录每个 Node 的信息。
            List<Node> Nodes = new List<Node>();

            Point p0, p1;
            double length = 0;
            int[] edges;
            Node n0, n1;
            #endregion

            #region Produce topology structure.
            for (int i = 0; i < mf.records.Count; i++)
            {
                PolyLine polyline = (mf.records[i].content as PolyLineRecordContent).PolyLine;
                for (int j = 0; j < polyline.NumParts; j++)
                {
                    int begin = polyline.Parts[j];
                    int end = (j == polyline.NumParts - 1) ? (polyline.NumPoints - 1) : (polyline.Parts[j + 1] - 1);
                    length = 0;
                    int ip0 = begin;
                    p0 = polyline.Points[ip0];
                    n0 = new Node(p0.X, p0.Y);
                    if (Nodes.Contains(n0))
                    {
                        n0 = Nodes[Nodes.IndexOf(n0)];
                        int count = n0.Edges.Length;
                        edges = new int[count + 1];
                        Array.Copy(n0.Edges, edges, count);
                        edges[count] = Edges.Count;
                        Nodes[Nodes.IndexOf(n0)] = new Node(n0.X, n0.Y, edges);
                    }
                    else
                    {
                        edges = new int[1];
                        edges[0] = Edges.Count;
                        n0.Edges = edges;
                        Nodes.Add(n0);
                    }

                    for (int k = begin + 1; k < end; k++)
                    {
                        p1 = polyline.Points[k];
                        length += Calculator.PointToPoint(p0, p1);

                        // 检查地图上通过 p1 的 PolyLine 的条线是否大于1。
                        if (IsThereAnotherPoint(i, k, mf))
                        {
                            // 如果是这样，说明 p1 是 Node。
                            // 记录 Node 信息。
                            n1 = new Node(p1.X, p1.Y);
                            if (Nodes.Contains(n1))
                            {
                                n1 = Nodes[Nodes.IndexOf(n1)];
                                int count = n1.Edges.Length;
                                edges = new int[count + 1];
                                Array.Copy(n1.Edges, edges, count);
                                edges[count] = Edges.Count;
                                Nodes[Nodes.IndexOf(n1)] = new Node(n1.X, n1.Y, edges);
                            }
                            else
                            {
                                edges = new int[1];
                                edges[0] = Edges.Count;
                                n1.Edges = edges;
                                Nodes.Add(n1);
                            }

                            // 记录 EdgeToPolyLine 信息。
                            int[] etpl = new int[3];
                            etpl[0] = i;
                            etpl[1] = ip0;
                            etpl[2] = k;
                            EdgeToPolyLine.Add(etpl);

                            // 记录 PolyLineToEdge 信息。
                            if (PolyLineToEdge[i] == null)
                            {
                                PolyLineToEdge[i] = new int[1];
                                PolyLineToEdge[i][0] = Edges.Count;
                            }
                            else
                            {
                                int count = PolyLineToEdge[i].Length;
                                edges = new int[count + 1];
                                Array.Copy(PolyLineToEdge[i], edges, count);
                                edges[count] = Edges.Count;
                                PolyLineToEdge[i] = edges;
                            }

                            // 记录 Edge 信息。
                            Edge edge = new Edge(Nodes.IndexOf(n0), Nodes.IndexOf(n1), length);
                            Edges.Add(edge);
                        }
                    }

                    //n1 = 
                    //if (Node)
               }
            }
            #endregion

            #region Save data to file.
            #endregion
        }

        private static bool IsThereAnotherPoint(int ipolyline, int ipoint, MainFile mf)
        {
            Point point = (mf.records[ipolyline].content as PolyLineRecordContent).PolyLine.Points[ipoint];

            for (int i = 0; i < mf.records.Count; i++)
            {
                PolyLine polyline = (mf.records[i].content as PolyLineRecordContent).PolyLine;
                for (int j = 0; j < polyline.NumPoints; j++)
                {
                    if (polyline.Points[j] == point && !(i == ipolyline && j == ipoint))
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 得到 .shp 地图文件对应的 .ngl 角度数据文件。
        /// </summary>
        /// <param name="shpFileName">要计算角度数据的地图文件名（*.shp）。</param>
        /// <param name="nglFileName">所生成的角度数据文件名（*.ngl）。</param>
        public static void ShpToNgl(string shpFileName, string nglFileName)
        {
            if (!File.Exists(shpFileName))
            {
                throw new FileNotFoundException();
            }

            #region Calculate angles.
            MainFile mf = new MainFile(shpFileName);
            int[][] Angles = new int[mf.records.Count][];
            for (int i = 0; i < mf.records.Count; i++)
            {
                PolyLine polyline = (mf.records[i].content as PolyLineRecordContent).PolyLine;
                Angles[i] = new int[polyline.NumPoints];
                for (int j = 0; j < polyline.NumParts; j++)
                {
                    int begin = polyline.Parts[j];
                    int end = (j == polyline.NumParts - 1) ? (polyline.NumPoints - 1) : (polyline.Parts[j + 1] - 1);
                    for (int k = begin; k < end; k++)
                    {
                        Point p1 = polyline.Points[k];
                        Point p2 = polyline.Points[k + 1];
                        double angle = Calculator.AngleOfVector(p2 - p1);
                        angle /= 2;
                        Angles[i][k] = Convert.ToInt32(angle);
                    }
                }
            }
            #endregion

            #region Write into file.
            try
            {
                FileStream fs = new FileStream(nglFileName, FileMode.Create);
                BinaryWriter bw = new BinaryWriter(fs);

                bw.Write(Angles.Length);
                for (int i = 0; i < Angles.Length; i++)
                {
                    bw.Write(Angles[i].Length);
                    for (int j = 0; j < Angles[i].Length; j++)
                    {
                        bw.Write(Angles[i][j]);
                    }
                }

                bw.Close();
                fs.Close();
            }
            catch (Exception ex)
            {
                //MessageBox.Show(ex.Message);
                Console.WriteLine("Error!" + ex.Message);
            }
            #endregion
        }
    }

    /// <summary>
    /// 各种用途的计算函数。
    /// </summary>
    public class Calculator
    {
        /// <summary>
        /// 求向量的角度，单位为角度（°）。为了与 GPS 数据统一，规定：正北方向为0°，顺时针方向为正。
        /// </summary>
        /// <param name="vertex">一个向量。</param>
        /// <returns>向量的角度（°）。</returns>
        public static double AngleOfVector(Point vector)
        {
            double angle = double.NaN;

            // 情况0：如果两个点重合，则返回 double.NaN （非数）。
            if (vector.X == 0 && vector.Y == 0)
            {
                angle = double.NaN;
            }

            double theta = Math.Atan(vector.Y / vector.X) * 180 / Math.PI;

            // 情况1：向量在第一象限。
            if (vector.X > 0 && vector.Y > 0)
                angle = 90 - theta;

            // 情况2：向量在第二象限。
            if (vector.X < 0 && vector.Y > 0)
                angle = 270 - theta;

            // 情况3：向量在第三象限。
            if (vector.X < 0 && vector.Y < 0)
                angle = 270 - theta;

            // 情况4：向量在第四象限。
            if (vector.X > 0 && vector.Y < 0)
                angle = 90 - theta;

            // 情况5：向量在 Y 轴正半轴上。
            if (vector.X == 0 && vector.Y > 0)
                angle = 0;

            // 情况6：向量在 X 轴负半轴上。
            if (vector.X < 0 && vector.Y == 0)
                angle = 270;

            // 情况7：向量在 Y 轴负半轴上。
            if (vector.X == 0 && vector.Y < 0)
                angle = 180;

            // 情况8：向量在 X 轴正半轴上。
            if (vector.X > 0 && vector.Y == 0)
                angle = 90;

            return angle;
        }

        /// <summary>
        /// 判断线段是否整个在矩形外。
        /// </summary>
        /// <param name="p1">线段的一个端点。</param>
        /// <param name="p2">线段的另一个端点。</param>
        /// <param name="left">矩形的左边界。</param>
        /// <param name="right">矩形的右边界。</param>
        /// <param name="top">矩形的上边界。</param>
        /// <param name="bottom">矩形的下边界。</param>
        /// <returns>线段是否在矩形外。</returns>
        public static bool IsSegmentOutOfRectangle(Point p1, Point p2, double left, double right, double top, double bottom)
        {
            bool b11 = p1.X < left;
            bool b12 = p1.X > right;
            bool b13 = p1.Y < bottom;
            bool b14 = p1.Y > top;
            bool b21 = p2.X < left;
            bool b22 = p2.X > right;
            bool b23 = p2.Y < bottom;
            bool b24 = p2.Y > top;

            return (b11 && b21) || (b12 && b22) || (b13 && b23) || (b14 && b24);
        }

        /// <summary>
        /// 求点到点的距离。
        /// </summary>
        /// <param name="p1">平面上一点。</param>
        /// <param name="p2">平面上另一点。</param>
        /// <returns>点到点的距离。</returns>
        public static double PointToPoint(Point p1, Point p2)
        {
            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// 求点到点的距离。
        /// </summary>
        /// <param name="x1">平面上一点的横坐标。</param>
        /// <param name="y1">平面上一点的纵坐标。</param>
        /// <param name="x2">平面上另一点的横坐标。</param>
        /// <param name="y2">平面上另一点的纵坐标。</param>
        /// <returns>点到点的距离</returns>
        public static double PointToPoint(double x1, double y1, double x2, double y2)
        {
            double dx = x2 - x1;
            double dy = y2 - y1;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// 求点到直线的距离。
        /// </summary>
        /// <param name="p">平面上一点。</param>
        /// <param name="p1">直线上一点。</param>
        /// <param name="p2">直线上另外一点。</param>
        /// <returns>点到直线的距离。</returns>
        public static double PointToLine(Point p, Point p1, Point p2)
        {
            double a = (p2.Y - p1.Y);
            double b = (p1.X - p2.X);
            if (a == 0 && b == 0)
            {
                return PointToPoint(p, p1);
            }

            double c = - p1.X * p2.Y + p2.X * p1.Y;
            double numerator = Math.Abs(a * p.X + b * p.Y + c);
            double denominator = Math.Sqrt(a * a + b * b);

            return numerator / denominator;
        }

        /// <summary>
        /// 求点到线段的距离。
        /// </summary>
        /// <param name="p">平面上一点。</param>
        /// <param name="p1">线段的一个端点。</param>
        /// <param name="p2">线段的另一个端点。</param>
        /// <returns>点到线段的距离。</returns>
        public static double PointToSegment(Point p, Point p1, Point p2)
        {
            // 情况0：点p1点p2重合
            Point p1p2 = p2 - p1;
            if (p1p2.X == 0 && p1p2.Y == 0)
            {
                return PointToPoint(p, p1);
            }

            // 情况1：点p在直线p1p2上的垂足落在点p1的外侧
            Point p1p = p - p1;
            double dotproduct1 = p1p * p1p2;
            if (dotproduct1 <= 0)
            {
                return PointToPoint(p, p1);
            }

            // 情况2：点p在直线p1p2上的垂足落在点p2的外侧
            Point p2p = p - p2;
            double dotproduct2 = p2p * p1p2;
            if (dotproduct2 >= 0)
            {
                return PointToPoint(p, p2);
            }

            // 情况3：点p在直线p1p2上的垂足落在线段p1p2上
            return PointToLine(p, p1, p2);
        }

        /// <summary>
        /// 求点到折线的距离，点到折线上与该点距离最近的线段之间的距离。
        /// </summary>
        /// <param name="p">平面上一点。</param>
        /// <param name="polyline">一条折线，折线由若干条线段首尾连接而成。</param>
        /// <returns>点到折线的距离。</returns>
        public static double PointToPolyLine(Point p, PolyLine polyline)
        {
            double min = double.PositiveInfinity;
            for (int j = 0; j < polyline.NumParts; j++)
            {
                int begin = polyline.Parts[j];
                int end = (j == polyline.NumParts - 1) ? (polyline.NumPoints - 1) : (polyline.Parts[j + 1] - 1);
                for (int k = begin; k < end; k++)
                {
                    double distance = PointToSegment(p, polyline.Points[k], polyline.Points[k + 1]);
                    min = (distance < min) ? distance : min;
                }
            }

            return min;
        }

        /// <summary>
        /// 求点到直线的垂足。
        /// </summary>
        /// <param name="p">平面上一点。</param>
        /// <param name="p1">直线上一点。</param>
        /// <param name="p2">直线上另一点。</param>
        /// <returns>垂足。</returns>
        public static Point NearestPointOnLine(Point p, Point p1, Point p2)
        {
            Point p1p2 = p2 - p1;
            if (p1p2.X == 0 && p1p2.Y == 0)
            {
                return p1;
            }

            Point p1p = p - p1;
            double dotproduct = p1p * p1p2;
            double norm = p1p2.Norm();

            return p1 + p1p2 * dotproduct / norm / norm;
        }

        /// <summary>
        /// 求线段上离某个点最近的点。
        /// </summary>
        /// <param name="p">平面上一点。</param>
        /// <param name="p1">线段的一个端点。</param>
        /// <param name="p2">线段的另一个端点。</param>
        /// <returns>最近的点。</returns>
        public static Point NearestPointOnSegment(Point p, Point p1, Point p2)
        {
            // 情况0：点p1与点p2重合
            Point p1p2 = p2 - p1;
            if (p1p2.X == 0 && p1p2.Y == 0)
            {
                return p1;
            }

            // 情况1：点p在直线p1p2上的垂足落在点p1的外侧
            Point p1p = p - p1;
            double dotproduct1 = p1p * p1p2;
            if (dotproduct1 <= 0)
            {
                return p1;
            }

            // 情况2：点p在直线p1p2上的垂足落在点p2的外侧
            Point p2p = p - p2;
            double dotproduct2 = p2p * p1p2;
            if (dotproduct2 >= 0)
            {
                return p2;
            }

            // 情况3：点p在直线p1p2上的垂足落在线段p1p2上
            return NearestPointOnLine(p, p1, p2);
        }

        /// <summary>
        /// 求折线上离某个点最近的点。
        /// </summary>
        /// <param name="p">平面上一点。</param>
        /// <param name="polyline">一条折线。</param>
        /// <returns>最近的点。</returns>
        public static Point NearestPointOnPolyLine(Point p, PolyLine polyline)
        {
            double min = double.PositiveInfinity;
            int index = -1;
            for (int j = 0; j < polyline.NumParts; j++)
            {
                int begin = polyline.Parts[j];
                int end = (j == polyline.NumParts - 1) ? (polyline.NumPoints - 1) : (polyline.Parts[j + 1] - 1);
                for (int k = begin; k < end; k++)
                {
                    double distance = PointToSegment(p, polyline.Points[k], polyline.Points[k + 1]);
                    if (distance <= min)
                    {
                        min = distance;
                        index = k;
                    }
                }
            }

            return NearestPointOnSegment(p, polyline.Points[index], polyline.Points[index + 1]);
        }

        /// <summary>
        /// 求距离指定点最近的折线（道路）的索引号，不使用网格索引文件（即遍历地图上所有的道路）。
        /// </summary>
        /// <param name="p">一个点（经度、纬度）。</param>
        /// <param name="mf">代表一个 .shp 文件。</param>
        /// <returns>索引号。</returns>
        public static int IndexOfNearestPolyLine(Point p, MainFile mf)
        {
            double min = double.PositiveInfinity;
            int index = -1;

            for (int i = 0; i < mf.records.Count; i++)
            {
                PolyLine polyline = (mf.records[i].content as PolyLineRecordContent).PolyLine;
                for (int j = 0; j < polyline.NumParts; j++)
                {
                    int begin = polyline.Parts[j];
                    int end = (j == polyline.NumParts - 1) ? (polyline.NumPoints - 1) : (polyline.Parts[j + 1] - 1);
                    for (int k = begin; k < end; k++)
                    {
                        double distance = PointToSegment(p, polyline.Points[k], polyline.Points[k + 1]);
                        if (distance <= min)
                        {
                            min = distance;
                            index = k;
                        }
                    }
                }
            }

            return index;
        }


        /// <summary>
        /// 求距离指定点最近的折线（道路）的索引号，不使用网格索引文件（即遍历地图上所有的道路）。
        /// override by Fenggang Wu Jun10 2013, wfg7530@163.com
        /// difference is that it can also return the corrected PGS position
        /// </summary>
        /// <param name="p">一个点（经度、纬度）。</param>
        /// <param name="mf">代表一个 .shp 文件。</param>
        /// <param name="outPoint">the corrected GPS point on road</param>
        /// <returns>索引号。</returns>
        public static int IndexOfNearestPolyLine(Point p, MainFile mf, ref Point outPoint)
        {
            double min = double.PositiveInfinity;
            int index = -1;
            PolyLine nearestPolyLine = default(PolyLine);

            for (int i = 0; i < mf.records.Count; i++)
            {
                PolyLine polyline = (mf.records[i].content as PolyLineRecordContent).PolyLine;
                for (int j = 0; j < polyline.NumParts; j++)
                {
                    int begin = polyline.Parts[j];
                    int end = (j == polyline.NumParts - 1) ? (polyline.NumPoints - 1) : (polyline.Parts[j + 1] - 1);
                    for (int k = begin; k < end; k++)
                    {
                        double distance = PointToSegment(p, polyline.Points[k], polyline.Points[k + 1]);
                        if (distance <= min)
                        {
                            min = distance;
                            index = k;
                            nearestPolyLine = polyline;
                        }
                    }
                }
            }

            outPoint = NearestPointOnPolyLine(p, nearestPolyLine);
            return index;
        }


        /// <summary>
        /// 求距离指定点最近的折线（道路）的索引号，使用网格索引文件。
        /// </summary>
        /// <param name="p">一个点（经度、纬度）。</param>
        /// <param name="mf">代表一个 .shp 文件。</param>
        /// <param name="gf">代表一个 .grd 文件。</param>
        /// <returns>索引号。</returns>
        public static int IndexOfNearestPolyLine(Point p, MainFile mf, GridFile gf)
        {
            double Xmin = mf.header.Xmin;
            double Xmax = mf.header.Xmax;
            double Ymin = mf.header.Ymin;
            double Ymax = mf.header.Ymax;
            double dx = (Xmax - Xmin) / gf.XGridNum;
            double dy = (Ymax - Ymin) / gf.YGridNum;
            int x0 = (int)((p.X - Xmin) / dx);
            int y0 = (int)((p.Y - Ymin) / dy);

            double min = double.PositiveInfinity;
            int index = -1;

            for (int x = x0 - 1; x <= x0 + 1; x++)
            {
                for (int y = y0 - 1; y <= y0 + 1; y++)
                {
                    for (int i = 0; i < gf.Grids[x, y].Length; i++)
                    {
                        int n = gf.Grids[x, y][i];
                        PolyLine polyline = (mf.records[n].content as PolyLineRecordContent).PolyLine;
                        double distance = PointToPolyLine(p, polyline);
                        if (distance <= min)
                        {
                            min = distance;
                            index = n;
                        }
                    }
                }
            }

            return index;
        }


        /// <summary>
        /// 求距离指定点最近的折线（道路）的索引号，使用网格索引文件。
        /// override by Fenggang Wu Jun10 2013, wfg7530@163.com
        /// difference is that it can also return the corrected PGS position
        /// </summary>
        /// <param name="p">一个点（经度、纬度）。</param>
        /// <param name="mf">代表一个 .shp 文件。</param>
        /// <param name="gf">代表一个 .grd 文件。</param>
        /// <param name="outPoint">the corrected GPS point on road</param>
        /// <returns>索引号。</returns>
        public static int IndexOfNearestPolyLine(Point p, MainFile mf, GridFile gf, ref Point outPoint)
        {
            double Xmin = mf.header.Xmin;
            double Xmax = mf.header.Xmax;
            double Ymin = mf.header.Ymin;
            double Ymax = mf.header.Ymax;
            double dx = (Xmax - Xmin) / gf.XGridNum;
            double dy = (Ymax - Ymin) / gf.YGridNum;
            int x0 = (int)((p.X - Xmin) / dx);
            int y0 = (int)((p.Y - Ymin) / dy);

            double min = double.PositiveInfinity;
            int index = -1;
            PolyLine nearestPolyLine = default(PolyLine);

            for (int x = x0 - 1; x <= x0 + 1; x++)
            {
                for (int y = y0 - 1; y <= y0 + 1; y++)
                {
                    for (int i = 0; i < gf.Grids[x, y].Length; i++)
                    {
                        int n = gf.Grids[x, y][i];
                        PolyLine polyline = (mf.records[n].content as PolyLineRecordContent).PolyLine;
                        double distance = PointToPolyLine(p, polyline);
                        if (distance <= min)
                        {
                            min = distance;
                            index = n;
                            nearestPolyLine = polyline;
                        }
                    }
                }
            }

            outPoint = NearestPointOnPolyLine(p, nearestPolyLine);
            return index;
        }


        /// <summary>
        /// 把一个 GPSRecord 定位到一条 PolyLine 上，考虑距离和角度，使用网格索引文件。
        /// </summary>
        /// <param name="p">要定位的 GPSRecord。</param>
        /// <param name="mf">代表一个 .shp 文件。</param>
        /// <param name="gf">代表一个 .grd 文件。</param>
        /// <returns>索引号。</returns>
        public static int IndexOfPreferredPolyLine(GPSRecord gr, MainFile mf, GridFile gf)
        {
            // 最大角度误差为30°。
            const double AngleError = 30;

            #region Initialization.
            double Xmin = mf.header.Xmin;
            double Xmax = mf.header.Xmax;
            double Ymin = mf.header.Ymin;
            double Ymax = mf.header.Ymax;
            double dx = (Xmax - Xmin) / gf.XGridNum;
            double dy = (Ymax - Ymin) / gf.YGridNum;
            int x0 = (int)((gr.Longitude - Xmin) / dx);
            int y0 = (int)((gr.Latitude - Ymin) / dy);
            #endregion

            #region Find preferred road.
            double min = double.PositiveInfinity;
            int road = -1;
            int segment = -1;
            for (int x = x0 - 1; x <= x0 + 1; x++)
            {
                for (int y = y0 - 1; y <= y0 + 1; y++)
                {
                    for (int i = 0; i < gf.Grids[x, y].Length; i++)
                    {
                        PolyLine polyline = (mf.records[gf.Grids[x, y][i]].content as PolyLineRecordContent).PolyLine;
                        for (int j = 0; j < polyline.NumParts; j++)
                        {
                            int begin = polyline.Parts[j];
                            int end = (j == polyline.NumParts - 1) ? (polyline.NumPoints - 1) : (polyline.Parts[j + 1] - 1);
                            for (int k = begin; k < end; k++)
                            {
                                Point p = new Point(gr.Longitude, gr.Latitude);
                                double distance = PointToSegment(p, polyline.Points[k], polyline.Points[k + 1]);
                                Point p1 = polyline.Points[k];
                                Point p2 = polyline.Points[k + 1];
                                double angle = AngleOfVector(p2 - p1);
                                double error = Math.Abs(gr.Angle * 2 - angle);
                                if (distance <= min && (error < AngleError || error > (180 - AngleError)))
                                {
                                    min = distance;
                                    road = gf.Grids[x, y][i];
                                    segment = k;
                                }
                            }
                        }
                    }
                }
            }
            #endregion

#if DEBUG
            Console.WriteLine("v" + gr.ID + ", r" + road + ", " + min * VpinSim.M_PER_DEG + "m");
#endif   
            return road;
        }

        /// <summary>
        /// 把一个 GPSRecord 定位到一条 PolyLine 上，考虑距离和角度，使用网格索引文件。
        /// override by Fenggang Wu Jun10 2013, wfg7530@163.com
        /// difference is that it can also return the corrected PGS position
        /// </summary>
        /// <param name="p">要定位的 GPSRecord。</param>
        /// <param name="mf">代表一个 .shp 文件。</param>
        /// <param name="gf">代表一个 .grd 文件。</param>
        /// <param name="outPoint">the corrected GPS point on road</param>
        /// <returns>索引号。</returns>
        public static int IndexOfPreferredPolyLine(GPSRecord gr, MainFile mf, GridFile gf, ref Point outPoint)
        {
            // 最大角度误差为30°。
            const double AngleError = 30;

            #region Initialization.
            double Xmin = mf.header.Xmin;
            double Xmax = mf.header.Xmax;
            double Ymin = mf.header.Ymin;
            double Ymax = mf.header.Ymax;
            double dx = (Xmax - Xmin) / gf.XGridNum;
            double dy = (Ymax - Ymin) / gf.YGridNum;
            int x0 = (int)((gr.Longitude - Xmin) / dx);
            int y0 = (int)((gr.Latitude - Ymin) / dy);
            #endregion

            #region Find preferred road.
            double min = double.PositiveInfinity;
            int road = -1;
            int segment = -1;
            for (int x = x0 - 1; x <= x0 + 1; x++)
            {
                for (int y = y0 - 1; y <= y0 + 1; y++)
                {
                    for (int i = 0; i < gf.Grids[x, y].Length; i++)
                    {
                        PolyLine polyline = (mf.records[gf.Grids[x, y][i]].content as PolyLineRecordContent).PolyLine;
                        for (int j = 0; j < polyline.NumParts; j++)
                        {
                            int begin = polyline.Parts[j];
                            int end = (j == polyline.NumParts - 1) ? (polyline.NumPoints - 1) : (polyline.Parts[j + 1] - 1);
                            for (int k = begin; k < end; k++)
                            {
                                Point p = new Point(gr.Longitude, gr.Latitude);
                                double distance = PointToSegment(p, polyline.Points[k], polyline.Points[k + 1]);
                                Point p1 = polyline.Points[k];
                                Point p2 = polyline.Points[k + 1];
                                double angle = AngleOfVector(p2 - p1);
                                double error = Math.Abs(gr.Angle * 2 - angle);
                                if (distance <= min && (error < AngleError || error > (180 - AngleError)))
                                {
                                    min = distance;
                                    road = gf.Grids[x, y][i];
                                    segment = k;
                                    outPoint = NearestPointOnSegment(p, polyline.Points[k], polyline.Points[k + 1]);
                                }
                            }
                        }
                    }
                }
            }
            #endregion

#if DEBUG
            Console.WriteLine("v" + gr.ID + ", r" + road + ", " + min * VpinSim.M_PER_DEG + "m");
#endif
            return road;
        }


        /// <summary>
        /// 把一个 GPSRecord 定位到一条 PolyLine 上，考虑距离和角度，使用网格索引文件和角度数据文件。
        /// override by Fenggang Wu Jun10 2013, wfg7530@163.com
        /// difference is that it does not return the corrected PGS position
        /// </summary>
        /// <param name="p">要定位的 GPSRecord。</param>
        /// <param name="mf">代表一个 .shp 文件。</param>
        /// <param name="gf">代表一个 .grd 文件。</param>
        /// <param name="af">代表一个 .ngl 文件。</param>
        /// <returns>索引号。</returns>
        public static int IndexOfPreferredPolyLine(GPSRecord gr, MainFile mf, GridFile gf, AngleFile af)
        {
            // 最大角度误差为30°。
            const int AngleError = 30;

            #region Initialization.
            double Xmin = mf.header.Xmin;
            double Xmax = mf.header.Xmax;
            double Ymin = mf.header.Ymin;
            double Ymax = mf.header.Ymax;
            double dx = (Xmax - Xmin) / gf.XGridNum;
            double dy = (Ymax - Ymin) / gf.YGridNum;
            int x0 = (int)((gr.Longitude - Xmin) / dx);
            int y0 = (int)((gr.Latitude - Ymin) / dy);
            #endregion

            #region Find preferred road.
            double min = double.PositiveInfinity;
            int road = -1;
            int segment = -1;
            for (int x = x0 - 1; x <= x0 + 1; x++)
            {
                for (int y = y0 - 1; y <= y0 + 1; y++)
                {
                    for (int i = 0; i < gf.Grids[x, y].Length; i++)
                    {
                        PolyLine polyline = (mf.records[gf.Grids[x, y][i]].content as PolyLineRecordContent).PolyLine;
                        for (int j = 0; j < polyline.NumParts; j++)
                        {
                            int begin = polyline.Parts[j];
                            int end = (j == polyline.NumParts - 1) ? (polyline.NumPoints - 1) : (polyline.Parts[j + 1] - 1);
                            for (int k = begin; k < end; k++)
                            {
                                Point p = new Point(gr.Longitude, gr.Latitude);
                                Point p1 = polyline.Points[k];
                                Point p2 = polyline.Points[k + 1];
                                double distance = PointToSegment(p, p1, p2);
                                int angle = af.Angles[gf.Grids[x, y][i]][k];
                                int error = Math.Abs(gr.Angle - angle) * 2;
                                if (distance <= min && (error < AngleError || error > (180 - AngleError)))
                                {
                                    //outPoint = NearestPointOnSegment(p, p1, p2);
                                    min = distance;
                                    road = gf.Grids[x, y][i];
                                    segment = k;
                                }
                            }
                        }
                    }
                }
            }
            #endregion

            return road;
        }

        /// <summary>
        /// 把一个 GPSRecord 定位到一条 PolyLine 上，考虑距离和角度，使用网格索引文件和角度数据文件。
        /// </summary>
        /// <param name="p">要定位的 GPSRecord。</param>
        /// <param name="mf">代表一个 .shp 文件。</param>
        /// <param name="gf">代表一个 .grd 文件。</param>
        /// <param name="af">代表一个 .ngl 文件。</param>
        /// <param name="outPoint">the corrected GPS point on road</param>
        /// <returns>索引号。</returns>
        public static int IndexOfPreferredPolyLine(GPSRecord gr, MainFile mf, GridFile gf, AngleFile af, ref Point outPoint)
        {
            // 最大角度误差为30°。
            const int AngleError = 30;

            #region Initialization.
            double Xmin = mf.header.Xmin;
            double Xmax = mf.header.Xmax;
            double Ymin = mf.header.Ymin;
            double Ymax = mf.header.Ymax;
            double dx = (Xmax - Xmin) / gf.XGridNum;
            double dy = (Ymax - Ymin) / gf.YGridNum;
            int x0 = (int)((gr.Longitude - Xmin) / dx);
            int y0 = (int)((gr.Latitude - Ymin) / dy);
            #endregion

            #region Find preferred road.
            double min = double.PositiveInfinity;
            int road = -1;
            int segment = -1;
            for (int x = x0 - 1; x <= x0 + 1; x++)
            {
                for (int y = y0 - 1; y <= y0 + 1; y++)
                {
                    for (int i = 0; i < gf.Grids[x, y].Length; i++)
                    {
                        PolyLine polyline = (mf.records[gf.Grids[x, y][i]].content as PolyLineRecordContent).PolyLine;
                        for (int j = 0; j < polyline.NumParts; j++)
                        {
                            int begin = polyline.Parts[j];
                            int end = (j == polyline.NumParts - 1) ? (polyline.NumPoints - 1) : (polyline.Parts[j + 1] - 1);
                            for (int k = begin; k < end; k++)
                            {
                                Point p = new Point(gr.Longitude, gr.Latitude);
                                Point p1 = polyline.Points[k];
                                Point p2 = polyline.Points[k + 1];
                                double distance = PointToSegment(p, p1, p2);
                                int angle = af.Angles[gf.Grids[x, y][i]][k];
                                int error = Math.Abs(gr.Angle - angle) * 2;
                                if (distance <= min && (error < AngleError || error > (180 - AngleError)))
                                {
                                    outPoint = NearestPointOnSegment(p, p1, p2);
                                    min = distance;
                                    road = gf.Grids[x, y][i];
                                    segment = k;
                                }
                            }
                        }
                    }
                }
            }
            #endregion
#if DEBUG
            Console.WriteLine("v" + gr.ID + ", r" + road + ", " + min * VpinSim.M_PER_DEG + "m");
#endif
            return road;
        }

        /// <summary>
        /// 求直线与直线的交点。平行返回：Point.None，重合返回：Point.Multipoint。
        /// </summary>
        /// <param name="p11">一条直线的一个端点。</param>
        /// <param name="p12">一条直线的另一个端点。</param>
        /// <param name="p21">另一条直线的一个端点。</param>
        /// <param name="p22">另一条直线的另一个端点。</param>
        /// <returns>两条直线的交点。</returns>
        public static Point LineCrossLine(Point p11, Point p12, Point p21, Point p22)
        {
            Point p = Point.None;

            // 求出两条直线方程的系数。直线标准方程：ax+by+c=0。
            double a1 = p12.Y - p11.Y;
            double b1 = p11.X - p12.X;
            double c1 = p12.X * p11.Y - p11.X * p12.Y;
            double a2 = p22.Y - p21.Y;
            double b2 = p21.X - p22.X;
            double c2 = p22.X * p21.Y - p21.X * p22.Y;
            double D = a1 * b2 - b1 * a2;
            double E = -c1 * b2 + c2 * b1;
            double F = -a1 * c2 + a2 * c1;

            // 情况1：两条直线平行。
            if (D == 0 && E != 0)
            {
                p = Point.None;   // 此返回值代表无交点。
            }

            // 情况2：两条直线重合。
            if (D == 0 && E == 0)
            {
                p = Point.Multipoint;   // 此返回值代表有无数交点。
            }

            // 情况3：两条直线不平行。
            if (D != 0)
            {
                p.X = E / D;
                p.Y = F / D;
            }

            return p;
        }

        /// <summary>
        /// 求线段与线段的交点。没有交点返回：Point.None，两条线段部分重合返回：Point.Multipoint。
        /// </summary>
        /// <param name="p11">一条线段的一个端点</param>
        /// <param name="p12">一条线段的另一个端点</param>
        /// <param name="p21">另一条线段的一个端点</param>
        /// <param name="p22">另一条线段的另一个端点</param>
        /// <returns>两条线段的交点。</returns>
        public static Point SegmentCrossSegment(Point p11, Point p12, Point p21, Point p22)
        {
            Point p = LineCrossLine(p11, p12, p21, p22);

            // 情况1：如果两条直线平行，则返回无交点。
            if (p == Point.None)
            {
                p = Point.None;
            }
            // 情况2：如果两条直线重合，则判断两条线段是否有交集。
            else if (p == Point.Multipoint)
            {
                // 线段不与 Y 轴平行
                if (p11.X != p12.X)
                {
                    double left = Math.Max(Math.Min(p11.X, p12.X), Math.Min(p21.X, p22.X));
                    double right = Math.Min(Math.Max(p11.X, p12.X), Math.Max(p21.X, p22.X));
                    if (left < right)
                    {
                        p = Point.Multipoint;
                    }
                    else if (left == right)
                    {
                        if (p11.X == left)
                            p = p11;
                        else if (p12.X == left)
                            p = p12;
                        else if (p21.X == left)
                            p = p21;
                        else if (p22.X == left)
                            p = p22;
                        else
                            //MessageBox.Show("Error!!!");
                            Console.WriteLine("Error!");
                    }
                    else
                    {
                        p = Point.None;
                    }
                }
                // 线段与 Y 轴平行
                else
                {
                    double left = Math.Max(Math.Min(p11.Y, p12.Y), Math.Min(p21.Y, p22.Y));
                    double right = Math.Min(Math.Max(p11.Y, p12.Y), Math.Max(p21.Y, p22.Y));
                    if (left < right)
                    {
                        p = Point.Multipoint;
                    }
                    else if (left == right)
                    {
                        if (p11.Y == left)
                            p = p11;
                        else if (p12.Y == left)
                            p = p12;
                        else if (p21.Y == left)
                            p = p21;
                        else if (p22.Y == left)
                            p = p22;
                        else
                            //MessageBox.Show("Error occurs in Calculator.SegmentCrossSegment().");
                            Console.WriteLine("Error occurs in Calculator.SegmentCrossSegment().");
                    }
                    else
                    {
                        p = Point.None;
                    }
                }
            }
            // 情况3：如果两条直线有交点，则判断交点是否均在两条线段上。
            else
            {
                if (IsPointInRectangle(p, p11, p12) && IsPointInRectangle(p, p21, p22))
                {
                    // p = p;
                }
                else
                {
                    p = Point.None; // 无交点。
                }
            }
            
            return p;
        }

        /// <summary>
        /// 辅助函数：判断点 p 是否在矩形内，上点p1和点p2是矩形的两个对角点。
        /// </summary>
        /// <param name="p">点。</param>
        /// <param name="p1">点。</param>
        /// <param name="p2">点。</param>
        /// <returns>是否。</returns>
        public static bool IsPointInRectangle(Point p, Point p1, Point p2)
        {
            return (p1.X - p.X) * (p.X - p2.X) >= 0 && (p1.Y - p.Y) * (p.Y - p2.Y) >= 0;
        }

        /// <summary>
        /// Determine whether a point <b>p</b> is in or on the circle
        /// centered at point <b>pc</b> with the radius <b>r</b>
        /// by Fenggang Wu Jun10,2013
        /// Email wfg7530@163.com
        /// </summary>
        /// <returns></returns>
        public static bool IsPointInCircle(Point p, Point pc, double r)
        {
            return PointToPoint(p, pc) <= r;
        }
    }

    /// <summary>
    /// 读取不同类型文件的函数。
    /// </summary>
    public class Reader
    {
        public static MainFileHeader ReadMainFileHeader(BinaryReader br)
        {
            MainFileHeader header = new MainFileHeader();

            header.FileCode = ReadBigEndianInt(br);
            header.Unused1 = ReadBigEndianInt(br);
            header.Unused2 = ReadBigEndianInt(br);
            header.Unused3 = ReadBigEndianInt(br);
            header.Unused4 = ReadBigEndianInt(br);
            header.Unused5 = ReadBigEndianInt(br);
            header.FileLength = ReadBigEndianInt(br);
            header.Version = br.ReadInt32();
            header.ShapeType = br.ReadInt32();
            header.Xmin = br.ReadDouble();
            header.Ymin = br.ReadDouble();
            header.Xmax = br.ReadDouble();
            header.Ymax = br.ReadDouble();
            header.Zmin = br.ReadDouble();
            header.Zmax = br.ReadDouble();
            header.Mmin = br.ReadDouble();
            header.Mmax = br.ReadDouble();

            return header;
        }

        public static int ReadBigEndianInt(BinaryReader br)
        {
            byte[] bytes = new byte[4];
            bytes = br.ReadBytes(4);
            Array.Reverse(bytes);

            return BitConverter.ToInt32(bytes, 0);
        }

        public static double ReadBigEndianDouble(BinaryReader br)
        {
            byte[] bytes = new byte[8];
            bytes = br.ReadBytes(8);
            Array.Reverse(bytes);

            return BitConverter.ToDouble(bytes, 0);
        }

        public static MainFileRecord ReadMainFileRecord(BinaryReader br)
        {
            MainFileRecord mfr = new MainFileRecord();
            mfr.header.RecrodNumber = ReadBigEndianInt(br);
            mfr.header.ContentLength = ReadBigEndianInt(br);
            mfr.content.ShapeType = br.ReadInt32();

            switch (mfr.content.ShapeType)
            {
                case 3:
                    PolyLineRecordContent content = new PolyLineRecordContent();
                    content.PolyLine = ReadPolyLine(br);
                    mfr.content = content;
                    break;
                default:
                    break;
            }

            return mfr;
        }

        public static PolyLine ReadPolyLine(BinaryReader br)
        {
            PolyLine PolyLine = new PolyLine();

            for (int i = 0; i < 4; i++)
            {
                PolyLine.Box[i] = br.ReadDouble();
            }
            PolyLine.NumParts = br.ReadInt32();
            PolyLine.NumPoints = br.ReadInt32();
            PolyLine.Parts = new int[PolyLine.NumParts];
            for (int i = 0; i < PolyLine.NumParts; i++)
            {
                PolyLine.Parts[i] = br.ReadInt32();
            }
            PolyLine.Points = new Point[PolyLine.NumPoints];
            for (int i = 0; i < PolyLine.NumPoints; i++)
            {
                PolyLine.Points[i] = new Point(br.ReadDouble(), br.ReadDouble());
            }

            return PolyLine;
        }

        public static IndexFileHeader ReadIndexFileHeader(BinaryReader br)
        {
            IndexFileHeader header = new IndexFileHeader();

            header.FileCode = ReadBigEndianInt(br);
            header.Unused1 = ReadBigEndianInt(br);
            header.Unused2 = ReadBigEndianInt(br);
            header.Unused3 = ReadBigEndianInt(br);
            header.Unused4 = ReadBigEndianInt(br);
            header.Unused5 = ReadBigEndianInt(br);
            header.FileLength = ReadBigEndianInt(br);
            header.Version = br.ReadInt32();
            header.ShapeType = br.ReadInt32();
            header.Xmin = br.ReadDouble();
            header.Ymin = br.ReadDouble();
            header.Xmax = br.ReadDouble();
            header.Ymax = br.ReadDouble();
            header.Zmin = br.ReadDouble();
            header.Zmax = br.ReadDouble();
            header.Mmin = br.ReadDouble();
            header.Mmax = br.ReadDouble();

            return header;
        }

        public static IndexRecord ReadIndexRecord(BinaryReader br)
        {
            IndexRecord record = new IndexRecord();

            record.Offset = ReadBigEndianInt(br);
            record.ContentLength = ReadBigEndianInt(br);

            return record;
        }

        public static GPSRecord ReadGPSRecord(StreamReader sr)
        {
            GPSRecord record = new GPSRecord();

            string line = sr.ReadLine();
            string[] tokens = line.Split(new char[] { ',' });
            record.ID = Convert.ToInt32(tokens[0]);
            record.TimeStamp = Convert.ToDateTime(tokens[1]);
            record.Longitude = Convert.ToDouble(tokens[2]);
            record.Latitude = Convert.ToDouble(tokens[3]);
            record.Speed = Convert.ToDouble(tokens[4]);
            record.Angle = Convert.ToInt32(tokens[5]);
            record.Status = IntToTaxiState(Convert.ToInt32(tokens[6]));

            return record;
        }

        private static TaxiState IntToTaxiState(int value)
        {
            TaxiState state = TaxiState.Vague;
            switch (value)
            {
                case 0:
                    state = TaxiState.Vacant;
                    break;
                case 1:
                    state = TaxiState.Occupied;
                    break;
                default:
                    state = TaxiState.Vague;
                    break;
            }

            return state;
        }
    }

    #region Structures and Classes.

    /// <summary>
    /// 代表一个 .shp 文件，包含解析该文件后得到的数据。
    /// </summary>
    public class MainFile
    {
        public readonly MainFileHeader header;
        public readonly List<MainFileRecord> records;

        public MainFile(string filename)
        {
            this.records = new List<MainFileRecord>();

            FileStream fs = new FileStream(filename, FileMode.Open);
            BinaryReader br = new BinaryReader(fs);

            this.header = Reader.ReadMainFileHeader(br);

            while (br.BaseStream.Position < br.BaseStream.Length)
            {
                this.records.Add(Reader.ReadMainFileRecord(br));
            }

            br.Close();
            fs.Close();
        }

        /// <summary>
        /// Open the mainfile (.shp file) <b>filename</b>, parse the road information
        /// Also, initialized the <b>roadDict</b> of the simulator
        /// </summary>
        /// <param name="filename">the mainfile .shp file name</param>
        /// <param name="dict">the roadDict that map road ID to road obj</param>
        public MainFile(string filename, VpinSim sim)
        {
            this.records = new List<MainFileRecord>();

            FileStream fs = new FileStream(filename, FileMode.Open);
            BinaryReader br = new BinaryReader(fs);

            this.header = Reader.ReadMainFileHeader(br);

            while (br.BaseStream.Position < br.BaseStream.Length)
            {
                this.records.Add(Reader.ReadMainFileRecord(br));
                sim.roadDict.Add(this.records.Count - 1, new Road(this.records.Count - 1));
            }

            br.Close();
            fs.Close();
        }

    }

    public class MainFileHeader
    {
        public int FileCode;    // Big
        public int Unused1;
        public int Unused2;
        public int Unused3;
        public int Unused4;
        public int Unused5;
        public int FileLength;  // Little
        public int Version;
        public int ShapeType;
        public double Xmin;
        public double Ymin;
        public double Xmax;
        public double Ymax;
        public double Zmin;
        public double Zmax;
        public double Mmin;
        public double Mmax;
    }

    public class MainFileRecord
    {
        public MainFileRecordHeader header;
        public MainFileRecordContent content;

        public MainFileRecord()
        {
            this.header = new MainFileRecordHeader();
            this.content = new MainFileRecordContent();
        }
    }

    public struct MainFileRecordHeader
    {
        public int RecrodNumber;
        public int ContentLength;
    }

    public class MainFileRecordContent
    {
        public int ShapeType;
    }

    public class PolyLineRecordContent : MainFileRecordContent
    {
        public PolyLine PolyLine;

        public PolyLineRecordContent()
        {
            this.ShapeType = 3;
            this.PolyLine = new PolyLine();
        }
    }

    public struct Point
    {
        public double X;
        public double Y;

        /// <summary>
        /// 代表没有点。
        /// </summary>
        public static Point None = new Point(double.PositiveInfinity, double.PositiveInfinity);

        /// <summary>
        /// 代表有许多个点。
        /// </summary>
        public static Point Multipoint = new Point(double.NegativeInfinity, double.NegativeInfinity);

        public Point(double x, double y)
        {
            this.X = x;
            this.Y = y;
        }

        public static Point operator +(Point p1, Point p2)
        {
            return new Point(p1.X + p2.X, p1.Y + p2.Y);
        }

        public static Point operator -(Point p1, Point p2)
        {
            return new Point(p1.X - p2.X, p1.Y - p2.Y);
        }

        public static double operator *(Point p1, Point p2)
        {
            return p1.X * p2.X + p1.Y * p2.Y;
        }

        public static Point operator *(Point p, double d)
        {
            return new Point(p.X * d, p.Y * d);
        }

        public static Point operator /(Point p, double d)
        {
            return new Point(p.X / d, p.Y / d);
        }

        public static bool operator ==(Point p1, Point p2)
        {
            return p1.X == p2.X && p1.Y == p2.Y;
        }

        public static bool operator !=(Point p1, Point p2)
        {
            return p1.X != p2.X || p1.Y != p2.Y;
        }

        public override string ToString()
        {
            string retval = "(" + this.X + ", " + this.Y + ")";
            if (this == Point.None)
                retval = "没有点";
            if (this == Point.Multipoint)
                retval = "多个点";

            return retval;
        }

        /// <summary>
        /// 求向量的模，即向量的长度。
        /// </summary>
        /// <returns>该向量的模。</returns>
        public double Norm()
        {
            return Math.Sqrt(this.X * this.X + this.Y * this.Y);
        }
    }

    public class PolyLine
    {
        public double[] Box = new double[4];
        public int NumParts;
        public int NumPoints;
        public int[] Parts;
        public Point[] Points;
    }

    /// <summary>
    /// 代表一个 .shx 文件，包含解析该文件后得到的数据。
    /// </summary>
    public class IndexFile
    {
        public readonly IndexFileHeader header;
        public readonly List<IndexRecord> records;

        public IndexFile(string filename)
        {
            this.records = new List<IndexRecord>();

            FileStream fs = new FileStream(filename, FileMode.Open);
            BinaryReader br = new BinaryReader(fs);

            this.header = Reader.ReadIndexFileHeader(br);

            while (br.BaseStream.Position < br.BaseStream.Length)
            {
                this.records.Add(Reader.ReadIndexRecord(br));
            }

            br.Close();
            fs.Close();
        }
    }

    public struct IndexFileHeader
    {
        public int FileCode;    // Big Endian
        public int Unused1;
        public int Unused2;
        public int Unused3;
        public int Unused4;
        public int Unused5;
        public int FileLength;  // Little Endian
        public int Version;
        public int ShapeType;
        public double Xmin;
        public double Ymin;
        public double Xmax;
        public double Ymax;
        public double Zmin;
        public double Zmax;
        public double Mmin;
        public double Mmax;
    }

    public struct IndexRecord
    {
        public int Offset;
        public int ContentLength;
    }

    /// <summary>
    /// 代表一个 .dbf 文件，包含解析该文件后得到的数据。
    /// </summary>
    public class dBASEFile
    {
        public readonly dBASERecord[] records;

        public dBASEFile(string filename)
        {
            int n = filename.LastIndexOf('\\');
            string path = filename.Substring(0, n);
            string tablename = filename.Substring(n + 1, filename.Length - path.Length - 5);
            try
            {
                string strConn = "Provider=Microsoft.Jet.OLEDB.4.0;Data Source=" + path + ";Extended Properties=dBASE IV";
                OleDbConnection conn = new OleDbConnection(strConn);
                conn.Open();
                OleDbDataAdapter adapter = new OleDbDataAdapter("SELECT * FROM " + tablename, conn);
                DataSet ds = new DataSet();
                adapter.Fill(ds);
                DataTable dt = ds.Tables[0];
                this.records = new dBASERecord[dt.Rows.Count];

                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    this.records[i] = new dBASERecord();
                    this.records[i].Fnode = Convert.ToInt32(dt.Rows[i].ItemArray[0]);
                    this.records[i].Tnode = Convert.ToInt32(dt.Rows[i].ItemArray[1]);
                    //this.records[i].Length = Convert.ToDouble(dt.Rows[i].ItemArray[2]);
                    this.records[i].RoadName = Convert.ToString(dt.Rows[i].ItemArray[3]);
                    this.records[i].InterRoadsName = Convert.ToString(dt.Rows[i].ItemArray[4]);
                    this.records[i].RoadType = Convert.ToString(dt.Rows[i].ItemArray[5]);
                    //this.records[i].ID = Convert.ToInt32(dt.Rows[i].ItemArray[6]);
                }

                dt.Dispose();
                ds.Dispose();
                conn.Close();
            }
            catch (Exception ex)
            {
                //MessageBox.Show(ex.Message);
                Console.WriteLine("Error! " + ex.Message);
            }
        }
    }

    public struct dBASERecord
    {
        public int Fnode;
        public int Tnode;
        public double Length;
        public string RoadName;
        public string InterRoadsName;
        public string RoadType;
        public int ID;
    }

    /// <summary>
    /// 代表一个追踪数据文件，包含解析该文件后得到的数据。
    /// </summary>
    public class GPSFile
    {
        public readonly List<GPSRecord> records;

        public GPSFile(string filename)
        {
            this.records = new List<GPSRecord>();

            FileStream fs = new FileStream(filename, FileMode.Open);
            StreamReader sr = new StreamReader(fs);

            while (sr.BaseStream.Position < sr.BaseStream.Length)
            {
                this.records.Add(Reader.ReadGPSRecord(sr));
            }

            sr.Close();
            fs.Close();
        }
    }

    public struct GPSRecord
    {
        public int ID;
        public DateTime TimeStamp;
        public double Longitude;
        public double Latitude;
        public double Speed;
        public int Angle;
        public TaxiState Status;
    }

    public enum TaxiState
    {
        Vacant,
        Occupied,
        Vague
    }

    /// <summary>
    /// 代表一个 .grd 文件，包含解析该文件后得到的数据。
    /// </summary>
    public class GridFile
    {
        public readonly int XGridNum;
        public readonly int YGridNum;

        public readonly int[,][] Grids;

        public GridFile(string filename)
        {
            //Debug//
            //int debug_count = 0;
            //EndDebug//
            FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read);
            BinaryReader br = new BinaryReader(fs);

            // 读文件头。
            this.XGridNum = br.ReadInt32();
            this.YGridNum = br.ReadInt32();
            this.Grids = new int[XGridNum, YGridNum][];
            // 读文件记录。
            for (int i = 0; i < this.XGridNum; i++)
            {
                for (int j = 0; j < this.YGridNum; j++)
                {
                    int count = br.ReadInt32();
                    //Debug//
                    //if (count == 0)
                    //{
                    //    debug_count++;
                    //}
                    //EndDebug//
                    this.Grids[i, j] = new int[count];
                    for (int k = 0; k < count; k++)
                    {
                        this.Grids[i, j][k] = br.ReadInt32();
                    }
                }
            }

            br.Close();
            fs.Close();
            //Debug//
            //MessageBox.Show(debug_count.ToString());
            //EndDebug//
        }
    }

    public class AngleFile
    {
        public readonly int[][] Angles;

        public AngleFile(string filename)
        {
            try
            {
                FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read);
                BinaryReader br = new BinaryReader(fs);

                this.Angles = new int[br.ReadInt32()][];
                for (int i = 0; i < this.Angles.Length; i++)
                {
                    this.Angles[i] = new int[br.ReadInt32()];
                    for (int j = 0; j < this.Angles[i].Length; j++)
                    {
                        this.Angles[i][j] = br.ReadInt32();
                    }
                }

                br.Close();
                fs.Close();
            }
            catch (Exception ex)
            {
                // MessageBox.Show(ex.Message);
                Console.WriteLine("Error! " + ex.Message);
            }
        }
    }

    /// <summary>
    /// 拓扑结构中的一条边。
    /// </summary>
    public struct Edge
    {
        public int FNode;
        public int TNode;
        public double Length;

        public Edge(int fNode, int tNode, double length)
        {
            this.FNode = fNode;
            this.TNode = tNode;
            this.Length = length;
        }

        public static bool operator ==(Edge e1, Edge e2)
        {
            return e1.FNode == e2.FNode && e1.TNode == e2.TNode && e1.Length == e2.Length;
        }

        public static bool operator !=(Edge e1, Edge e2)
        {
            return e1.FNode != e2.FNode || e1.TNode != e2.TNode || e1.Length != e2.Length;
        }
    }

    /// <summary>
    /// 拓扑结构中的一个节点。
    /// </summary>
    public struct Node
    {
        public double X;
        public double Y;
        public int[] Edges;

        public Node(double x, double y)
        {
            this.X = x;
            this.Y = y;
            this.Edges = null;
        }

        public Node(double x, double y, int[] edges)
        {
            this.X = x;
            this.Y = y;
            this.Edges = edges;
        }

        public static bool operator ==(Node n1, Node n2)
        {
            return n1.X == n2.X && n1.Y == n2.Y;
        }

        public static bool operator !=(Node n1, Node n2)
        {
            return n1.X != n2.X || n1.Y != n2.Y;
        }
    }

    #endregion

}