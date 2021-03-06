﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;
using VisualProcessors.Processing;

namespace VisualProcessors.Processors
{
	[ProcessorMeta("Bram Kamies", "Multiplies two values together", "A", "Output",
		CustomTabMode = ProcessorVisibility.Hide)]
	public class MultiplyProcessor : Processor
	{
		#region Properties

		#endregion Properties

		#region Constructor

		public MultiplyProcessor()
		{
		}

		public MultiplyProcessor(Pipeline pipeline, string name)
			: base(pipeline, name)
		{
			AddInputChannel("A", false);
			AddInputChannel("B", false);
			AddOutputChannel("Output");
		}

		#endregion Constructor

		#region Methods

		protected override void Process()
		{
			double a = GetInputChannel("A").GetValue();
			double b = GetInputChannel("B").GetValue();
			GetOutputChannel("Output").WriteValue(a * b);
		}

		#endregion Methods
	}
}