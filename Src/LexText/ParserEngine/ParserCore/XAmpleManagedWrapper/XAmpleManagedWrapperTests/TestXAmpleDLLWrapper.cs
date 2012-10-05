using System;
using System.IO;
using NUnit.Framework;
using XAmpleManagedWrapper;

namespace XAmpleManagedWrapperTests
{


	[TestFixture]
	public class TestXAmpleDLLWrapper
	{

		protected XAmpleDLLWrapper CreateXAmpleDllWrapper()
		{
			var xAmple = new XAmpleDLLWrapper();
			xAmple.Init(""); // assume libample.so/XAMPLE.DLL is in the same directory.
			return xAmple;
		}

		protected void LoadFilesHelper(XAmpleDLLWrapper wrapper)
		{
			string tempPath = "../../Src/LexText/ParserEngine/ParserCore/ParserCoreTests/M3ToXAmpleTransformerTestsDataFiles";
			// TODO: use DirectoryFinder.FWCodeDirectory
			string xPath = "../../DistFiles/" + "/Language Explorer/Configuration/Grammar";
			wrapper.LoadFiles(xPath, tempPath, "StemName3");
		}

		[Test]
		// FWNX-556 Reenable after upgrade Linux build machine
		[Category("ByHand")]
		public void TestInit ()
		{
			using (XAmpleDLLWrapper wrapper = CreateXAmpleDllWrapper())
				Assert.IsNotNull(wrapper);
		}

		[Test]
		// FWNX-556 Reenable after upgrade Linux build machine
		[Category("ByHand")]
		public void TestLoadFiles ()
		{
			using (XAmpleDLLWrapper wrapper = CreateXAmpleDllWrapper())
			LoadFilesHelper(wrapper);
		}

		[Test]
		// FWNX-556 Reenable after upgrade Linux build machine
		[Category("ByHand")]
		public void TestSetParameter()
		{
			using (XAmpleDLLWrapper wrapper = CreateXAmpleDllWrapper())
			wrapper.SetParameter("MaxAnalysesToReturn", "3");
		}

		[Test]
		// FWNX-556 Reenable after upgrade Linux build machine
		[Category("ByHand")]
		public void TestGetSetup()
		{
			using (XAmpleDLLWrapper wrapper = CreateXAmpleDllWrapper())
			Assert.AreNotEqual(IntPtr.Zero, wrapper.GetSetup());
		}

		[Test]
		// FWNX-556 Reenable after upgrade Linux build machine
		[Category("ByHand")]
		[ExpectedException(typeof(NotImplementedException))]
		public void TestSetLogFile()
		{
			using (XAmpleDLLWrapper wrapper = CreateXAmpleDllWrapper())
			wrapper.SetLogFile(Path.GetTempFileName());
		}

		[Test]
		// FWNX-556 Reenable after upgrade Linux build machine
		[Category("ByHand")]
		public void GetAmpleThreadId()
		{
			using (XAmpleDLLWrapper wrapper = CreateXAmpleDllWrapper())
			{
			int threadId = wrapper.GetAmpleThreadId();
#if __MonoCS__
			Assert.AreEqual(0, threadId);
#else
			Assert.AreNotEqual(0, threadId);
#endif
		}
		}

		[Test]
		// FWNX-556 Reenable after upgrade Linux build machine
		[Category("ByHand")]
		public void TestParseString()
		{

			using (XAmpleDLLWrapper wrapper = CreateXAmpleDllWrapper())
			{
			LoadFilesHelper(wrapper);
			string parsedString = wrapper.ParseString("Hello");
			Assert.IsNotEmpty(parsedString);
			Assert.IsNotNull(parsedString);
		}
		}

		[Test]
		// FWNX-556 Reenable after upgrade Linux build machine
		[Category("ByHand")]
		public void TestTraceString()
		{
			using (XAmpleDLLWrapper wrapper = CreateXAmpleDllWrapper())
			{
			LoadFilesHelper(wrapper);
			string tracedString = wrapper.TraceString("Hello", "Hello");
			Assert.IsNotEmpty(tracedString);
			Assert.IsNotNull(tracedString);
		}
	}
	}
}