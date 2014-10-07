﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;
using VisualProcessors;
using VisualProcessors.Controls;
using VisualProcessors.Forms;
using VisualProcessors.Processing;

namespace VisualProcessors
{
	internal static class Program
	{
		/// <summary>
		///  The main entry point for the application.
		/// </summary>
		[STAThread]
		private static void Main()
		{
			Pipeline p = new Pipeline();

			DirectInputProcessor in1 = new DirectInputProcessor("DirectInputProcessor1");
			CodeProcessor code1 = new CodeProcessor("CodeProcessor1");
			GraphPlotter gplot = new GraphPlotter("GraphPlotter1");
			in1.GetOutputChannel("Output").Link(code1.GetInputChannel("A"));
			code1.GetOutputChannel("Output1").Link(gplot.GetInputChannel("Red"));
			p.AddProcessor(in1);
			p.AddProcessor(code1);
			p.AddProcessor(gplot);

			string path = Application.StartupPath + "/output.xml";
			FileStream file = new FileStream(path, FileMode.Create);

			p.SaveToFile(file);
			file.Position = 0;

			var dp = Pipeline.LoadFromFile(file);

			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			Application.Run(new PipelineForm(dp));
		}
	}
}