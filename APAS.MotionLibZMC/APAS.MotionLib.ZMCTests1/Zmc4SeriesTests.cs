using Microsoft.VisualStudio.TestTools.UnitTesting;
using APAS.MotionLib.ZMC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APAS.MotionLib.ZMC.Tests
{
	[TestClass()]
	public class Zmc4SeriesTests
	{
		[TestMethod()]
		public void Zmc4SeriesTest()
		{
			var mc = new Zmc4Series("192.168.0.11", 0, "Zmc4SeriesConf.json,");
            mc.UnitTest(0);

		}

		[TestMethod()]
		public void ACSMotionTest()
		{
			var X = 0;
			var Y = 1;
			var acs = new ACS.ACS("10.0.0.100", 701);
			acs.UnitTestMotion(X);
		}
	}
}