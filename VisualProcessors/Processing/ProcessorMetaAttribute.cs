﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace VisualProcessors.Processing
{
	public enum ProcessorTabs
	{
		Input,
		Properties,
		Output,
		Custom,
	}

	public enum ProcessorThreadMode
	{
		ForceMultiThreading,
		DenyMultiThreading,
		UserDefined,
	}

	public enum ProcessorVisibility
	{
		Show,
		Hide,
	}

	[AttributeUsage(AttributeTargets.Class)]
	public class ProcessorMeta : System.Attribute
	{
		public static ProcessorMeta Default = new ProcessorMeta("Unknown", "Unknown", "Unknown", "Unknown");

		/// <summary>
		///  Determines whether the input channels are allowed to be optional
		/// </summary>
		public bool AllowOptionalInputs = false;

		/// <summary>
		///  Determines whether the user can spawn this processor on demand
		/// </summary>
		public bool AllowUserSpawn = true;

		/// <summary>
		///  Determines the author of the processor
		/// </summary>
		public string Author;

		/// <summary>
		///  Determines the display of the 'Custom' TabPage on the ProcessorForm of this type of
		///  Processor
		/// </summary>
		public ProcessorVisibility CustomTabMode = ProcessorVisibility.Show;

		/// <summary>
		///  Determines the title of the 'Custom' tab
		/// </summary>
		public string CustomTabTitle = "Custom";

		/// <summary>
		///  Determines the name of the default/primary InputChannel
		/// </summary>
		public string DefaultInput;

		/// <summary>
		///  Determines the name of the default/primary OutputChannel
		/// </summary>
		public string DefaultOutput;

		/// <summary>
		///  A short description of the processor
		/// </summary>
		public string Description;

		/// <summary>
		///  Determines the display of the 'Input' TabPage on the ProcessorForm of this type of
		///  Processor
		/// </summary>
		public ProcessorVisibility InputTabMode = ProcessorVisibility.Show;

		/// <summary>
		///  Determines the title of the 'Input' tab
		/// </summary>
		public string InputTabTitle = "Input";

		/// <summary>
		///  Determines the display of the 'Output' TabPage on the ProcessorForm of this type of
		///  Processor
		/// </summary>
		public ProcessorVisibility OutputTabMode = ProcessorVisibility.Show;

		/// <summary>
		///  Determines the title of the 'Output' tab
		/// </summary>
		public string OutputTabTitle = "Output";

		/// <summary>
		///  Determines the display of the 'Properties' TabPage on the ProcessorForm of this type of
		///  Processor
		/// </summary>
		public ProcessorVisibility PropertiesTabMode = ProcessorVisibility.Show;

		/// <summary>
		///  Determines the title of the 'Properties' tab
		/// </summary>
		public string PropertiesTabTitle = "Properties";

		/// <summary>
		///  Determines whether the custom tab should be displayed on the ProcessorDataForm
		/// </summary>
		public ProcessorVisibility DataFormMode = ProcessorVisibility.Hide;

		/// <summary>
		///  Determines whether the custom tab is shown on a seperate form on the PipelineOutputForm
		/// </summary>
		public bool ShowOutputWindow = false;

		/// <summary>
		///  Determines whether to use multithreading, also allows the end user to choose
		/// </summary>
		public ProcessorThreadMode ThreadMode = ProcessorThreadMode.UserDefined;

		public ProcessorMeta(string author, string description, string defaultinput, string defaultoutput)
		{
			Author = author;
			Description = description;
			DefaultInput = defaultinput;
			DefaultOutput = defaultoutput;
		}
	}
}