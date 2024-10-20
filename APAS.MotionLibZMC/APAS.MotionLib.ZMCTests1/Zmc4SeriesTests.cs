using Microsoft.VisualStudio.TestTools.UnitTesting;
using APAS.MotionLib.ZMC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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
			
			var portNameSimulator = "SIMULATOR"; // 连接到模拟器
			var portNameIP = new IPAddress([10, 0, 0, 100]).ToString(); // 连接到真实IP
			var X = 0; // X轴编号
			var Y = 1; // Y轴编号
			// var acs = new ACS.ACS("10.0.0.100", 701);
			var acs = new ACS.ACS(portNameSimulator, 701);
			acs.UnitTestMotion(X);
		}
	}
}