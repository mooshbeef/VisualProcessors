﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using VisualProcessors.Processing;
using VisualProcessors.Processors;

namespace VisualProcessors.Forms
{
	public partial class PipelineDataForm : Form
	{
		#region Properties

		private Dictionary<Processor, ProcessorInterfaceForm> m_Interfaces = new Dictionary<Processor, ProcessorInterfaceForm>();
		private PipelineForm m_Master;

		public PipelineForm Master
		{
			get
			{
				return m_Master;
			}
			private set
			{
				if (m_Master != null)
				{
					m_Master.PipelineChanged -= MasterPipelineChanged;
				}
				m_Master = value;
				if (m_Master != null)
				{
					m_Master.PipelineChanged += MasterPipelineChanged;
				}
			}
		}

		#endregion Properties

		#region Constructor

		public PipelineDataForm()
		{
			InitializeComponent();
		}

		public PipelineDataForm(PipelineForm master)
			: this()
		{
			Master = master;
			MasterPipelineChanged(null, master.CurrentPipeline);
		}

		#endregion Constructor

		#region Methods

		private void AddProcessorInterface(Processor proc)
		{
			if (m_Interfaces.ContainsKey(proc))
			{
				RemoveProcessorInterface(proc);
			}
			if (proc.Meta.DataFormMode == ProcessorVisibility.Show)
			{
				ProcessorInterfaceForm gf = new ProcessorInterfaceForm(proc);
				m_Interfaces.Add(proc, gf);
				gf.TopLevel = false;
				gf.MdiParent = this;
				gf.Show();
				var item = showProcessorToolStripMenuItem.DropDownItems.Add(proc.Name, null, ShowProcessorDropDownItemClick);
				item.Name = proc.Name;//Used for RemoveByKey()
			}
		}

		protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
		{
			if (keyData == (Keys.Control | Keys.Space))
			{
				Master.HideDataWindow();
				return true;
			}
			return base.ProcessCmdKey(ref msg, keyData);
		}

		private void RemoveProcessorInterface(Processor proc)
		{
			ProcessorInterfaceForm gf = m_Interfaces[proc];
			gf.Close();
			gf.Dispose();
			m_Interfaces.Remove(proc);
			showProcessorToolStripMenuItem.DropDownItems.RemoveByKey(proc.Name);
		}

		#endregion Methods

		#region Event Handlers

		private void MasterPipelineChanged(Pipeline arg1, Pipeline arg2)
		{
			if (arg1 != null)
			{
				arg1.ProcessorAdded -= AddProcessorInterface;
				arg1.ProcessorRemoved -= RemoveProcessorInterface;
				while (m_Interfaces.Count > 0)
				{
					RemoveProcessorInterface(m_Interfaces.ElementAt(0).Key);
				}
			}
			if (arg2 != null)
			{
				arg2.ProcessorAdded += AddProcessorInterface;
				arg2.ProcessorRemoved += RemoveProcessorInterface;
				foreach (string name in arg2.GetListOfNames())
				{
					AddProcessorInterface(arg2.GetByName(name));
				}
			}
		}

		private void ShowProcessorDropDownItemClick(object sender, EventArgs e)
		{
			if (Master.CurrentPipeline != null)
			{
				AddProcessorInterface(Master.CurrentPipeline.GetByName((sender as ToolStripDropDownItem).Text));
			}
		}

		#endregion Event Handlers

		private void arrangeToolStripMenuItem_Click(object sender, EventArgs e)
		{
			LayoutMdi(MdiLayout.ArrangeIcons);
		}

		private void cascadeToolStripMenuItem_Click(object sender, EventArgs e)
		{
			LayoutMdi(MdiLayout.Cascade);
		}

		private void tileHorizontallyToolStripMenuItem_Click(object sender, EventArgs e)
		{
			LayoutMdi(MdiLayout.TileHorizontal);
		}

		private void tileVerticallyToolStripMenuItem_Click(object sender, EventArgs e)
		{
			LayoutMdi(MdiLayout.TileVertical);
		}
	}
}