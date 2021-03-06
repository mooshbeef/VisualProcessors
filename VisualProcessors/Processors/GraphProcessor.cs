﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;
using VisualProcessors.Processing;
using ZedGraph;

namespace VisualProcessors.Processors
{
	[ProcessorMeta("Bram Kamies", "Plots its InputChannels onto a ZedGraph in realtime", "Red", "",
		AllowOptionalInputs = true,
		CustomTabTitle = "Graph",
		OutputTabMode = ProcessorVisibility.Hide,
		DataFormMode=ProcessorVisibility.Show,
		ShowOutputWindow = true)]
	public class GraphProcessor : Processor
	{
		#region Properties

		private long m_Counter = 0;
		private List<ZedGraphControl> m_Graphs = new List<ZedGraphControl>();
		private RollingPointPairList m_PointListBlue;
		private RollingPointPairList m_PointListGreen;
		private RollingPointPairList m_PointListRed;
		private bool m_Redraw = false;
		private Thread m_RenderThread;
		private DateTime m_StartTimestamp;

		#endregion Properties

		#region Options

		[Browsable(true)]
		[ReadOnly(false)]
		[DisplayName("Buffer size")]
		[Category("Settings")]
		[Description("The amount of values to store per curve")]
		[DefaultValue(200)]
		public int BufferSize
		{
			get
			{
				return int.Parse(Options.GetOption("BufferSize", "200"));
			}
			set
			{
				Options.SetOption("BufferSize", value.ToString());
				IsPrepared = false;
				OnModified(HaltTypes.ShouldHalt);
			}
		}

		[Browsable(true)]
		[ReadOnly(false)]
		[DisplayName("ForceScroll")]
		[Category("Settings")]
		[Description("When true, the graph will scroll in real-time showing the last received samples. When false, the graph will autozoom to fit the curve entirely on the panel")]
		[DefaultValue(true)]
		public bool ForceScroll
		{
			get
			{
				return bool.Parse(Options.GetOption("ForceScroll", "True"));
			}
			set
			{
				Options.SetOption("ForceScroll", value.ToString());
				OnModified(HaltTypes.Continue);
			}
		}

		[Browsable(true)]
		[ReadOnly(false)]
		[DisplayName("RealTime")]
		[Category("Settings")]
		[Description("When true, the X-axis represents time in seconds. When false the X-axis represents the index of the Y-value")]
		[DefaultValue(true)]
		public bool RealTime
		{
			get
			{
				return bool.Parse(Options.GetOption("RealTime", "False"));
			}
			set
			{
				Options.SetOption("RealTime", value.ToString());
				OnModified(HaltTypes.Ask);
			}
		}

		#endregion Options

		#region Constructor

		public GraphProcessor()
			: base()
		{
		}

		public GraphProcessor(Pipeline pipeline, string name)
			: base(pipeline, name)
		{
			AddInputChannel("Red", false);
			AddInputChannel("Blue", false);
			AddInputChannel("Green", false);
		}

		private void SetupLists()
		{
			List<ZedGraphControl> copy = m_Graphs.ToList();
			m_Graphs.Clear();
			m_PointListRed = new RollingPointPairList(BufferSize);
			m_PointListBlue = new RollingPointPairList(BufferSize);
			m_PointListGreen = new RollingPointPairList(BufferSize);
			while (copy.Count > 0)
			{
				ZedGraphControl graph = copy[0];
				copy.RemoveAt(0);
				if (!graph.BeenDisposed)
				{
					graph.GraphPane.CurveList.Clear();
					var rinput = GetInputChannel("Red");
					var binput = GetInputChannel("Blue");
					var ginput = GetInputChannel("Green");
					graph.GraphPane.AddCurve(rinput.IsConnected ? rinput.Source.Name : "Disconnected", m_PointListRed, Color.Red, SymbolType.None);
					graph.GraphPane.AddCurve(binput.IsConnected ? binput.Source.Name : "Disconnected", m_PointListBlue, Color.Blue, SymbolType.None);
					graph.GraphPane.AddCurve(ginput.IsConnected ? ginput.Source.Name : "Disconnected", m_PointListGreen, Color.Green, SymbolType.None);
					m_Graphs.Add(graph);
				}
			}
		}

		#endregion Constructor

		#region Methods

		public override void GetUserInterface(Panel panel)
		{
			ZedGraphControl graph = new ZedGraphControl();
			graph.Dock = DockStyle.Fill;
			panel.Controls.Add(graph);
			m_Graphs.Add(graph);
			base.GetUserInterface(panel);
			SetupLists();
		}

		public override void Start()
		{
			if (m_RenderThread != null)
			{
				Stop();
			}
			m_RenderThread = new Thread(new ThreadStart(RenderThreadMethod));
			m_RenderThread.IsBackground = true;
			m_RenderThread.Start();
			base.Start();
		}

		public override void Stop()
		{
			if (m_RenderThread != null)
			{
				if (m_RenderThread.IsAlive)
				{
					m_RenderThread.Abort();
				}
				while (m_RenderThread.IsAlive) ;
				m_RenderThread = null;
			}
			base.Stop();
		}

		protected override void Prepare()
		{
			m_StartTimestamp = DateTime.Now;
			m_Counter = 0;
			SetupLists();
			base.Prepare();
		}

		protected override void Process()
		{
			lock (this)
			{
				if (RealTime)
				{
					ReadAndWrite(GetInputChannel("Red"), m_PointListRed);
					ReadAndWrite(GetInputChannel("Blue"), m_PointListBlue);
					ReadAndWrite(GetInputChannel("Green"), m_PointListGreen);
				}
				else
				{
					bool again = true;
					while (again)
					{
						var inputRed = GetInputChannel("Red");
						if (inputRed.HasValue())
						{
							m_PointListRed.Add(m_Counter, inputRed.GetValue());
							again &= inputRed.HasValue();
						}
						var inputBlue = GetInputChannel("Blue");
						if (inputBlue.HasValue())
						{
							m_PointListBlue.Add(m_Counter, inputBlue.GetValue());
							again &= inputBlue.HasValue();
						}
						var inputGreen = GetInputChannel("Green");
						if (inputGreen.HasValue())
						{
							m_PointListGreen.Add(m_Counter, inputGreen.GetValue());
							again &= inputGreen.HasValue();
						}
						m_Counter++;
					}
				}
			}
			m_Redraw = true;
		}

		private void ReadAndWrite(InputChannel source, RollingPointPairList list)
		{
			double val = 0;
			while (source.HasValue())
			{
				val = source.GetValue();
				list.Add(DateTime.Now.Subtract(m_StartTimestamp).TotalSeconds, val);
			}
		}

		private void RenderThreadMethod()
		{
			while (m_Graphs.Count > 0)
			{
				if (m_Redraw)
				{
					lock (this)
					{
						foreach (ZedGraphControl graph in m_Graphs)
						{
							if (graph.IsDisposed)
							{
								m_Graphs.Remove(graph);
								break;
							}
							if (ForceScroll)
							{
								if (RealTime)
								{
									graph.GraphPane.XAxis.Scale.Min = DateTime.Now.Subtract(m_StartTimestamp).TotalSeconds - 2;
									graph.GraphPane.XAxis.Scale.Max = DateTime.Now.Subtract(m_StartTimestamp).TotalSeconds;
								}
								else
								{
									graph.GraphPane.XAxis.Scale.Min = m_Counter - BufferSize;
									graph.GraphPane.XAxis.Scale.Max = m_Counter;
								}
							}
							graph.AxisChange();
							graph.Invalidate();
							m_Redraw = false;
						}
					}
				}
				Thread.Sleep(16);
			}
		}

		#endregion Methods
	}
}