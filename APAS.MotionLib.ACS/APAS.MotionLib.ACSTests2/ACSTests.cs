using Microsoft.VisualStudio.TestTools.UnitTesting;
using APAS.MotionLib.ACS;

namespace APAS.MotionLib.ACS.Tests
{
    [TestClass()]
    public class ACSTests
    {
        [TestMethod()]
        public void UnitTestAnalogTest()
        {
            var mc = new ACS("SIMULATOR", 7701);
            mc.AreaScanZigZag(0, 1, 100, 5, 10);

        }
    }
}