using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using Microsoft.Win32.SafeHandles;
namespace HCore
{
    public class HLine
    {
        
        private HObject _line;
        public HObject Line 
        {
            get { return _line; }
            set { _line = value; }
        }
        
        private List<Point> _points;
        public List<Point> Points
        { 
            get 
            {
                if (!Line.IsInitialized())
                    return null;

                return _points; 
            }
            set
            {
                _points = value;
                CreatLineSegment();
            }
            
        }
        public HObject CreatLineSegment()
        {
            HTuple rows = new HTuple();
            HTuple cols = new HTuple();
            HTuple nr = new HTuple();
            HTuple nc = new HTuple();
            HTuple Dist = new HTuple();
            HTuple rowBegin = new HTuple();
            HTuple rowEnd = new HTuple();
            HTuple colBegin = new HTuple();
            HTuple colEnd = new HTuple();
            for (int i = 0; i < _points.Count(); i++)
            {
                rows.Append(_points[i].Y);
                cols.Append(_points[i].X);
            }
            HObject l = new HObject();
            HOperatorSet.GenContourPolygonXld(out l, rows, cols);
            HOperatorSet.FitLineContourXld(l, "tukey", -1, 0, 5, 2, out rowBegin, out colBegin, out rowEnd, out colEnd, out nr, out nc, out Dist);
            rows = null; cols = null;
            rows.Append(rowBegin);
            rows.Append(rowEnd);
            cols.Append(colBegin);
            cols.Append(colEnd);
            HOperatorSet.GenContourPolygonXld(out _line, rows, cols);
            rows.Dispose();
            cols.Dispose();
            nr.Dispose();
            nc.Dispose();
            Dist.Dispose();
            rowBegin.Dispose();
            rowEnd.Dispose();
            colBegin.Dispose();
            colEnd.Dispose();
            l.Dispose();
            return _line;
        }
        public void Dispose()
        {
            Line.Dispose();
            
            _line.Dispose();
            Points.Clear();
            _points.Clear();
        }
    }
}
