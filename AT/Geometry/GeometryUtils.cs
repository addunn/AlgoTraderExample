using System;
using System.Collections.Generic;
using System.Linq;
using AT.Geometry.Objects;

namespace AT.Geometry
{
    public class Utils
    {

        public static Line GetBestFitLine(List<Point> points)
        {
            if (points.Count == 0)
            {
                return null;
            }

            if (points.Count == 1)
            {
                return new Line { Slope = 0, YIntercept = points[0].Y } ;
            }
            else {
                int numPoints = points.Count;

                decimal meanX = points.Average(p => p.X);

                decimal meanY = points.Average(p => p.Y);

                BigDecimal sumXSquared = 0;

                for (int n = 0; n < points.Count; n++)
                {
                    // CL.BigDecimal x = ;
                    sumXSquared += (BigDecimal)(points[n].X * points[n].X);
                }

                BigDecimal sumXY = 0;

                for (int n = 0; n < points.Count; n++)
                {
                    BigDecimal x = points[n].X;
                    BigDecimal y = points[n].Y;
                    sumXY += x * y;
                }


                BigDecimal bd1 = (sumXSquared / numPoints);
                BigDecimal bd2 = meanX * meanX;
                
                BigDecimal slope = ((sumXY / numPoints - meanX * meanY)) / (bd1 - bd2);
                BigDecimal yI = (meanY - (slope * meanX));
               
                if (slope < 0.2)
                {
                    slope = slope.Truncate((27 - ((-1 * slope.Exponent) - slope.Mantissa.ToString().Length)));
                }
                else
                {
                    slope = slope.Truncate();
                }
    
                yI = yI.Truncate();

                return new Line { Slope = Math.Round((decimal)slope, 20), YIntercept = Math.Round((decimal)yI, 20) };
            }
        }


    }
}
