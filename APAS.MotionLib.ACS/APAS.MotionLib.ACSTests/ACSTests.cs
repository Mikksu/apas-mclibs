using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace APAS.MotionLib.ACS.Tests
{
	[TestClass()]
	public class ACSTests
	{
		[TestMethod()]
		public void UnitTestTest()
		{
			var acs = new ACS("10.0.0.100", 701, "", null);
			acs.UnitTestMotion();
		}

		[TestMethod()]
		public void AnalogInputReadTest()
		{
			var acs = new ACS("10.0.0.100", 701, "", null);
			acs.UnitTestAnalog();
			
		}

		[TestMethod()]
		public void Fast1DTest()
		{
			var acs = new ACS("10.0.0.100", 701, "", null);
			acs.UnitTestFast1D();

		}
	}
}