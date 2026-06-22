using Common.Utils.Extensions;

namespace Common.Utils.Helpers;

public static class RateCalculationHelper
{
    /// <summary>
    /// %100 percent calculation
    /// </summary>
    /// <param name="value1">count</param>
    /// <param name="value2">totalCount</param>
    /// <returns></returns>
    public static int Calc(int value1, int value2)
    {
        if (value2 == 0)
        {
            return 0;
        }
        
        return ((decimal)value1 / value2 * 100).ToRound();
    }
}
