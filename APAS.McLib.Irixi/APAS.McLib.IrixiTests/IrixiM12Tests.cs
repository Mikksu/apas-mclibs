using Microsoft.VisualStudio.TestTools.UnitTesting;
using APAS.McLib.Irixi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APAS.McLib.Irixi.Tests
{
	[TestClass()]
	public class IrixiM12Tests
	{
		[TestMethod()]
		public void UtMotionTest()
		{
			var mc = new IrixiM12("COM7", 115200, "", null);
			mc.UtMotion();
		}
	}
}