﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.WindowsAPICodePack.Taskbar;
using VisualProcessors;
using VisualProcessors.Controls;
using VisualProcessors.Forms;
using VisualProcessors.Processing;

namespace VisualProcessors.Forms
{
	public partial class PipelineForm : Form
	{
		#region Properties

		private bool m_IsSaved = false;
		private MdiClient m_MdiClient;
		private MdiClientHelper m_MdiClientHelper;
		private Pipeline m_Pipeline;

		public Pipeline CurrentPipeline
		{
			get
			{
				return m_Pipeline;
			}
			set
			{
				SetPipeline(value);
			}
		}

		public bool IsSaved
		{
			get
			{
				return m_IsSaved;
			}
			set
			{
				string str = Path.GetFileName(m_CurrentFilepath);
				if (str == "")
				{
					str = "Unnamed";
				}
				Text = ((value) ? "" : "*") + str;
				m_IsSaved = value;
			}
		}

		public MdiClient MdiClient
		{
			get
			{
				return m_MdiClient;
			}
		}

		#endregion Properties

		#region Constructor

		public PipelineForm()
		{
			InitializeComponent();

			//Update MenuItems
			CurrentPipeline = null;

			//Get MdiClient
			foreach (Control c in Controls)
			{
				if (c is MdiClient)
				{
					m_MdiClient = c as MdiClient;
					break;
				}
			}
			MdiClient.Paint += MdiClientPaintLinks;
			MdiClient.MouseDown += MdiClientMouseDown;
			MdiClient.MouseMove += MdiClientMouseMove;
			MdiClient.MouseUp += MdiClientMouseUp;
			MdiClient.MouseClick += MdiClientMouseClick;
			m_MdiClientHelper = new MdiClientHelper(m_MdiClient);

			//Toolbox
			Toolbox.PipelineForm = this;
			ErrorList.Master = this;
		}

		#endregion Constructor

		#region Methods

		public void SetPipeline(Pipeline pipeline)
		{
			Pipeline old = CurrentPipeline;
			if (CurrentPipeline != null)
			{
				CurrentPipeline.Started -= PipelineStarted;
				CurrentPipeline.Stopped -= PipelineStopped;
				CurrentPipeline.ProcessorModified -= PipelineModification;
				CurrentPipeline.Error -= PipelineError;
				CurrentPipeline.Stop();
			}
			foreach (ProcessorForm pf in m_ProcessorForms)
			{
				pf.ForceClose();
			}
			m_ProcessorForms.Clear();
			m_Pipeline = pipeline;
			if (CurrentPipeline != null)
			{
				CurrentPipeline.Started += PipelineStarted;
				CurrentPipeline.Stopped += PipelineStopped;
				CurrentPipeline.ProcessorModified += PipelineModification;
				CurrentPipeline.Error += PipelineError;
				CurrentPipeline.Stop();
				foreach (string name in pipeline.GetListOfNames())
				{
					AddProcessor(pipeline.GetByName(name), true);
				}
				SimulationStatusLabel.Text = "Edit-mode";
			}
			else
			{
				SimulationStatusLabel.Text = "No file";
			}
			bool enabled = (CurrentPipeline != null);
			closeToolStripMenuItem.Enabled = enabled;
			saveToolStripMenuItem.Enabled = enabled;
			saveAsToolStripMenuItem.Enabled = enabled;
			runToolStripMenuItem.Enabled = enabled;
			resetToolStripMenuItem.Enabled = enabled;
			loadStateToolStripMenuItem.Enabled = enabled;
			saveStateToolStripMenuItem.Enabled = enabled;

			OnPipelineChanged(old, CurrentPipeline);
			if (MdiClient != null)
			{
				MdiClient.Invalidate();
			}
		}

		private Point CalculateChannelLink(Form a, Point end)
		{
			Point result = new Point();
			Point start = a.GetCenter();
			Point delta = new Point(end.X - start.X, end.Y - start.Y);
			Point absdelta = new Point(Math.Abs(end.X - start.X), Math.Abs(end.Y - start.Y));
			if (absdelta.X > absdelta.Y)
			{
				if (delta.X > 0)
				{
					//A is left of B
					result = new Point(a.Right, (a.Top + a.Bottom) / 2);
				}
				else
				{
					//A is right of B
					result = new Point(a.Left, (a.Top + a.Bottom) / 2);
				}
			}
			else
			{
				if (delta.Y > 0)
				{
					//A is below B
					result = new Point((a.Left + a.Right) / 2, a.Bottom);
				}
				else
				{
					//A is above B
					result = new Point((a.Left + a.Right) / 2, a.Top);
				}
			}

			return result;
		}

		public void StartSimulation()
		{
			if (CurrentPipeline != null)
			{
				runToolStripMenuItem.Checked = true;
				CurrentPipeline.Start();
			}
		}
		public void StopSimulation()
		{
			if (CurrentPipeline != null)
			{
				runToolStripMenuItem.Checked = false;
				CurrentPipeline.Stop();
			}
		}
		public void ResetSimulation()
		{
			if (CurrentPipeline != null)
			{
				runToolStripMenuItem.Checked = false;
				CurrentPipeline.Reset();
			}
		}
		public bool IsSimulationRunning()
		{
			return CurrentPipeline != null && (CurrentPipeline.IsRunning || CurrentPipeline.IsPreparingSimulation);
		}

		#endregion Methods

		#region ProcessorForm Control

		private List<ProcessorForm> m_ProcessorForms = new List<ProcessorForm>();

		/// <summary>
		///  Adds a new processor to the pipeline, creates a ProcessorForm for it, and shows the
		///  form on the MDIClient
		/// </summary>
		/// <param name="p">       The processor to add to the underlaying pipeline</param>
		/// <param name="useModel">
		///  When true, the new ProcessorForm will have the size and position that the given
		///  Processor describes, used when loading models from XML data
		/// </param>
		/// <returns>The (new) ProcessorForm that owns the processor</returns>
		public ProcessorForm AddProcessor(Processor p, bool useModel)
		{
			if (GetProcessorForm(p.Name) == null)
			{
				ProcessorForm f = new ProcessorForm(this, p, useModel);
				m_ProcessorForms.Add(f);
				f.TopLevel = false;
				f.MdiParent = this;
				f.Show();
			}
			if (!CurrentPipeline.IsProcessorNameTaken(p.Name))
			{
				CurrentPipeline.AddProcessor(p);
			}
			return GetProcessorForm(p.Name);
		}

		/// <summary>
		///  Gets the form that owns a named processor
		/// </summary>
		/// <param name="name">The name of the processor</param>
		/// <returns>The form that owns the processor, or null if none is found</returns>
		public ProcessorForm GetProcessorForm(string name)
		{
			foreach (ProcessorForm pf in m_ProcessorForms)
			{
				if (pf.Processor.Name == name)
				{
					return pf;
				}
			}
			return null;
		}

		/// <summary>
		///  Hides a ProcessorForm by making it invisible
		/// </summary>
		/// <param name="name">The name of the processor that has to be hidden</param>
		public void HideProcessor(string name)
		{
			ProcessorForm pf = GetProcessorForm(name);
			if (pf != null)
			{
				pf.Visible = false;
			}
		}

		/// <summary>
		///  Checks if a Processor's form is (partially) visible in the MdiClient
		/// </summary>
		/// <param name="processorname">
		///  The name of the processor whose ProcessorForm to check
		/// </param>
		/// <returns>
		///  Returns true if the processor has a ProcessorForm that is visible in the MdiClient,
		///  false otherwise
		/// </returns>
		public bool IsProcessorVisible(string processorname)
		{
			ProcessorForm pf = GetProcessorForm(processorname);
			if (pf == null)
			{
				return false;
			}
			return IsProcessorVisible(pf);
		}

		/// <summary>
		///  Checks if a given ProcessorForm is (partially) visible in the MdiClient
		/// </summary>
		/// <param name="form"></param>
		/// <returns>
		///  Returns true if the ProcessorForm is (partially) visible in the MdiClient, false
		///  otherwise
		/// </returns>
		public bool IsProcessorVisible(ProcessorForm form)
		{
			return form.Left < MdiClient.Width && form.Top < MdiClient.Height && form.Right > 0 && form.Bottom > 0;
		}

		/// <summary>
		///  Removes a processor from the pipeline
		/// </summary>
		/// <param name="pform">The name of the processor that has to be remoed</param>
		/// <returns>True when succesfull, false otherwise</returns>
		public bool RemoveProcessor(string name)
		{
			ProcessorForm pf = GetProcessorForm(name);
			if (pf == null)
			{
				return false;
			}
			CurrentPipeline.RemoveProcessor(pf.Processor);
			pf.Processor.Dispose();
			bool result = m_ProcessorForms.Remove(pf);
			pf.Close();
			pf.Dispose();
			return result;
		}

		public void ShowInputChannel(string processorname, string channelname)
		{
			ProcessorForm pf = GetProcessorForm(processorname);
			if (pf != null)
			{
				pf.ShowInputChannel(channelname);
			}
		}

		/// <summary>
		///  Centers the view on a given processor
		/// </summary>
		/// <param name="name"> Name of the processor to focus on</param>
		/// <param name="bring">
		///  When false, it will move the viewport to center on the form, when true, it will move
		///  the form to the center of the viewport
		/// </param>
		public void ShowProcessor(string name)
		{
			ProcessorForm pf = GetProcessorForm(name);
			pf.Visible = true;
			pf.Focus();
			Point centerView = m_MdiClient.DisplayRectangle.GetCenter();
			Point centerForm = pf.GetCenter();
			Point offset = new Point(centerView.X - centerForm.X, centerView.Y - centerForm.Y);
			foreach (ProcessorForm form in m_ProcessorForms)
			{
				Point loc = form.Location;
				loc.Offset(offset);
				form.Location = loc;
			}
		}

		#endregion ProcessorForm Control

		#region Processor Linking

		private string m_LinkChannelName;
		private LinkMode m_LinkMode = LinkMode.Disabled;
		private ProcessorForm m_LinkStart;

		/// <summary>
		///  Completes the current linking session, if the linkmode is set to OutputFirst, this will
		///  also show a dialog box to the user to select the InputChannel to use. When the linkmode
		///  is set to InputFirst, no dialog box is shown because the InputChannel was selected at
		///  the start of the linking session.
		/// </summary>
		/// <param name="second">The ProcessorForm that contains the endpoint of the link</param>
		public void CompleteLinkMode(ProcessorForm second)
		{
			try
			{
				string cname = null;
				if (m_LinkMode == LinkMode.InputFirst && second.Processor.OutputChannelCount == 1)
				{
					cname = second.Processor.GetOutputChannelNames()[0];
				}
				else if (m_LinkMode == LinkMode.OutputFirst && second.Processor.InputChannelCount == 1)
				{
					cname = second.Processor.GetInputChannelNames()[0];
				}
				else
				{
					ChannelSelectionBox csbox = new ChannelSelectionBox((m_LinkMode == LinkMode.InputFirst) ? ChannelType.OutputChannel : ChannelType.InputChannel, second.Processor);
					if (csbox.ShowDialog(this) == DialogResult.Cancel)
					{
						return;
					}
					cname = csbox.Choice;
				}
				InputChannel input = null;
				OutputChannel output = null;
				if (m_LinkMode == LinkMode.InputFirst)
				{
					input = m_LinkStart.Processor.GetInputChannel(m_LinkChannelName);
					output = second.Processor.GetOutputChannel(cname);
				}
				else
				{
					input = second.Processor.GetInputChannel(cname);
					output = m_LinkStart.Processor.GetOutputChannel(m_LinkChannelName);
				}
				if (input == null)
				{
					MessageBox.Show("The InputChannel was not found!", "Link error", MessageBoxButtons.OK);
					return;
				}
				if (output == null)
				{
					MessageBox.Show("The OutputChannel was not found!", "Link error", MessageBoxButtons.OK);
					return;
				}
				output.Link(input);
			}
			finally
			{
				ResetLinkMode();
				MdiClient.Invalidate();
			}
		}

		/// <summary>
		///  Stops the current linking session if one exists
		/// </summary>
		public void ResetLinkMode()
		{
			foreach (ProcessorForm pform in m_ProcessorForms)
			{
				pform.SetLinkMode(LinkMode.Disabled, false);
			}
			m_LinkMode = LinkMode.Disabled;
		}

		public void StartLinkMode(ProcessorForm first, string channelname, LinkMode mode)
		{
			if (m_LinkMode != LinkMode.Disabled)
			{
				ResetLinkMode();
			}
			if (mode == LinkMode.Disabled)
			{
				return;
			}
			foreach (ProcessorForm pform in m_ProcessorForms)
			{
				pform.SetLinkMode(mode, pform == first);
			}
			m_LinkStart = first;
			m_LinkChannelName = channelname;
			m_LinkMode = mode;
		}

		#endregion Processor Linking

		#region Events

		public event Action<Pipeline, Pipeline> PipelineChanged;

		private void OnPipelineChanged(Pipeline oldPipeline, Pipeline newPipeline)
		{
			if (PipelineChanged != null)
			{
				PipelineChanged(oldPipeline, newPipeline);
			}
		}

		#endregion Events

		#region EventHandlers

		private Point m_ContextLocation;
		private bool m_IsDragging = false;
		private Point m_PreviousDragPosition;
		private bool m_PaintingLinks = false;

		private void MdiClientContextMenuOpening(object sender, CancelEventArgs e)
		{
			AddMenu.DropDownItems.Clear();
			foreach (Type t in Toolbox.Types.Reverse())
			{
				AddMenu.DropDownItems.Add(t.Name, null, delegate(object _sender, EventArgs _e)
				{
					Toolbox.SpawnProcessor(t, this.m_ContextLocation);
				});
			}
			GotoMenu.DropDownItems.Clear();
			BringMenu.DropDownItems.Clear();
			foreach (ProcessorForm pf in m_ProcessorForms)
			{
				GotoMenu.DropDownItems.Add(pf.Processor.Name, null, delegate(object _sender, EventArgs _e)
				{
					ShowProcessor(pf.Processor.Name);
					MdiClient.Invalidate();
				});
				BringMenu.DropDownItems.Add(pf.Processor.Name, null, delegate(object _sender, EventArgs _e)
				{
					Point offset = new Point(-pf.Width / 2, -pf.Height / 2);
					m_ContextLocation.Offset(offset);
					pf.Location = m_ContextLocation;
					MdiClient.Invalidate();
				});
			}
		}

		private void PipelineFormLoad(object sender, EventArgs e)
		{
			if (Program.Config.StartMaximized)
			{
				this.WindowState = FormWindowState.Maximized;
			}
			if (Program.Config.ShowDataFormOnStartup)
			{
				ShowDataWindow();
			}
			if (Program.Config.OpenLastPipelineOnStartup)
			{
				LoadFile(Program.Config.LastOpenedPipelineFile);
			}
			this.SetBevel(Program.Config.Use3DBorderOnMdiClient);
			InitializeThumbnail();
		}

		private void MdiClientMouseClick(object sender, MouseEventArgs e)
		{
			if (e.Button.HasFlag(MouseButtons.Right))
			{
				m_ContextLocation = e.Location;
				MdiClientContextMenu.Show(m_MdiClient, e.Location);
			}
		}

		private void MdiClientMouseDown(object sender, MouseEventArgs e)
		{
			m_IsDragging = true;
			m_PreviousDragPosition = e.Location;
			Cursor = Cursors.Hand;
		}

		private void MdiClientMouseMove(object sender, MouseEventArgs e)
		{
			if (!m_IsDragging)
			{
				return;
			}
			this.SuspendLayout();
			Point offset = new Point(e.Location.X - m_PreviousDragPosition.X, e.Location.Y - m_PreviousDragPosition.Y);
			foreach (ProcessorForm pf in m_ProcessorForms)
			{
				Point pos = pf.Location;
				pos.Offset(offset);
				pf.Location = pos;
			}
			this.ResumeLayout(true);
			m_PreviousDragPosition.Offset(offset);
		}

		private void MdiClientMouseUp(object sender, MouseEventArgs e)
		{
			m_IsDragging = false;
			Cursor = Cursors.Default;
		}

		private void MdiClientPaintLinks(object sender, PaintEventArgs e)
		{
#warning Optimization needed
			if (m_PaintingLinks)
			{
				return;
			}
			m_PaintingLinks = true;
			BufferedGraphicsContext currentContext;
			BufferedGraphics myBuffer;
			currentContext = BufferedGraphicsManager.Current;
			myBuffer = currentContext.Allocate(m_MdiClient.CreateGraphics(), m_MdiClient.DisplayRectangle);
			if (CurrentPipeline != null)
			{
				if (CurrentPipeline.IsRunning)
				{
					myBuffer.Graphics.Clear(Program.Config.SimulationBackgroundColor);
				}
				else
				{
					myBuffer.Graphics.Clear(Program.Config.WorksheetBackgroundColor);
				}
			}
			else
			{
				myBuffer.Graphics.Clear(Color.Gray);
			}

			Pen outputhalf = new Pen(Color.Red, 4);
			Pen inputhalf = new Pen(Color.Green, 4);
			Pen unused = new Pen(Color.Gray, 4);
			System.Drawing.Font font = new System.Drawing.Font("Arial", 14, FontStyle.Bold);
			foreach (ProcessorForm startform in m_ProcessorForms)
			{
				if (!startform.Visible)
				{
					continue;
				}
				foreach (string outputname in startform.Processor.GetOutputChannelNames())
				{
					OutputChannel outputchannel = startform.Processor.GetOutputChannel(outputname);
					foreach (InputChannel inputchannel in outputchannel.Targets)
					{
						if (outputchannel.Owner != inputchannel.Owner)
						{
							ProcessorForm endform = GetProcessorForm(inputchannel.Owner.Name);
							if (!endform.Visible)
							{
								continue;
							}
							if (IsProcessorVisible(startform) || IsProcessorVisible(endform))
							{
								Point lineoutput = CalculateChannelLink(startform, endform.GetCenter());
								Point lineinput = CalculateChannelLink(endform, lineoutput);
								Point halfway = new Point((lineoutput.X + lineinput.X) / 2, (lineoutput.Y + lineinput.Y) / 2);
								myBuffer.Graphics.DrawLine(outputhalf, lineoutput, halfway);
								myBuffer.Graphics.DrawLine((inputchannel.IsConstant) ? unused : inputhalf, halfway, lineinput);
								string str1 = "(" + outputchannel.Name + ")";
								string str2 = "(" + inputchannel.Name + ")";
								SizeF size1 = myBuffer.Graphics.MeasureString(str1, font);
								SizeF size2 = myBuffer.Graphics.MeasureString(str2, font);
								float height = size1.Height + size2.Height;
								myBuffer.Graphics.DrawString(str1, font, new SolidBrush(outputhalf.Color), halfway.X - size1.Width / 2, halfway.Y - height / 2);
								myBuffer.Graphics.DrawString(str2, font, new SolidBrush((inputchannel.IsConstant) ? unused.Color : inputhalf.Color), halfway.X - size2.Width / 2, halfway.Y + size2.Height - height / 2);
							}
						}
					}
				}
			}
			myBuffer.Render();
			myBuffer.Dispose();
			m_PaintingLinks = false;
		}

		private void PipelineError(object sender, ProcessorErrorEventArgs e)
		{
			if (sender is Processor)
			{
				Processor p = sender as Processor;
				MethodInvoker action = delegate
				{
					ShowProcessor(p.Name);
					ErrorIcon.Clear();
					ErrorIcon.SetError(GetProcessorForm(p.Name), e.Title + Environment.NewLine + e.Message);
					ErrorIcon.Icon = e.IsWarning ? SystemIcons.Warning : SystemIcons.Error;
				};
				this.BeginInvoke(action);
			}
			if (e.HaltType != HaltTypes.Continue && (CurrentPipeline.IsRunning || CurrentPipeline.IsPreparingSimulation))
			{
				if ((e.HaltType == HaltTypes.ShouldHalt) || (MessageBox.Show(e.Message + Environment.NewLine + "Do you want to stop the simulation?", e.Title, MessageBoxButtons.YesNo) == System.Windows.Forms.DialogResult.Yes))
				{
					CurrentPipeline.Stop();
				}
			}
		}

		private void PipelineFormFormClosing(object sender, FormClosingEventArgs e)
		{
			if (CurrentPipeline != null)
			{
				CurrentPipeline.Stop();
			}
		}

		private void PipelineModification(object sender, ProcessorModifiedEventArgs e)
		{
			MethodInvoker action = delegate
			{
				IsSaved = false;
				if (e.HaltType == HaltTypes.ShouldHalt)
				{
					CurrentPipeline.Stop();
				}
			};
			this.BeginInvoke(action);
		}

		private void PipelineStarted(object sender, EventArgs e)
		{
			MethodInvoker action = delegate
			{
				runToolStripMenuItem.Checked = true;
				SimulationStatusLabel.Text = "Simulating";
				MdiClient.Invalidate();
			};
			this.BeginInvoke(action);
		}

		private void PipelineStopped(object sender, EventArgs e)
		{
			MethodInvoker action = delegate
			{
				runToolStripMenuItem.Checked = false;
				SimulationStatusLabel.Text = "Edit-mode";
				MdiClient.Invalidate();
			};
			this.BeginInvoke(action);
		}

		#endregion EventHandlers

		#region MenuStrip Implementation

		#region Menu: File

		private string m_CurrentFilepath = "";

		public string CurrentFilepath
		{
			get
			{
				return m_CurrentFilepath;
			}
		}

		public void CreateNew()
		{
			CurrentPipeline = new Pipeline();
			IsSaved = true;
		}

		public void LoadFile()
		{
			MainMenuOpenFileDialog.InitialDirectory = m_CurrentFilepath;
			MainMenuOpenFileDialog.Filter = "Pipeline file|*" + Program.Config.PipelineFileExtension + "|XML-file|*.xml|All files|*.*";
			DialogResult result = MainMenuOpenFileDialog.ShowDialog();
			if (result == DialogResult.Cancel)
			{
				return;
			}
			LoadFile(MainMenuOpenFileDialog.FileName);
		}

		public void LoadFile(string path)
		{
			Text = "Pipeline Editor";
			FileStream file = new FileStream(path, FileMode.Open);
			try
			{
				CurrentPipeline = Pipeline.LoadFromFile(file);
				m_CurrentFilepath = path;
				Program.Config.LastOpenedPipelineFile = path;
			}
			catch (Exception e)
			{
				while (e.InnerException != null)
				{
					e = e.InnerException;
				}
				MessageBox.Show("There was a XML syntax error when trying to parse the file:" + Environment.NewLine + e.Message, "Syntax error");
			}
			finally
			{
				file.Close();
				IsSaved = true;
			}
		}

		public void SaveFile()
		{
			if (m_CurrentFilepath != "")
			{
				FileStream file = new FileStream(m_CurrentFilepath, FileMode.Create);
				CurrentPipeline.SaveToFile(file);
				file.Close();
				IsSaved = true;
			}
			else
			{
				m_CurrentFilepath = SaveFileAs();
			}
		}

		public string SaveFileAs()
		{
			MainMenuSaveFileDialog.InitialDirectory = m_CurrentFilepath;
			MainMenuSaveFileDialog.Filter = "Pipeline file|*" + Program.Config.PipelineFileExtension + "|XML-file|*.xml|All files|*.*";
			DialogResult result = MainMenuSaveFileDialog.ShowDialog();
			if (result != DialogResult.Cancel)
			{
				m_CurrentFilepath = MainMenuSaveFileDialog.FileName;
				SaveFile();
			}
			return m_CurrentFilepath;
		}

		private void closeToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (CurrentPipeline != null && !IsSaved && !ConfirmNoSave())
			{
				return;
			}
			CurrentPipeline = null;
		}

		private bool ConfirmNoSave()
		{
			DialogResult result = MessageBox.Show("Do you want to save?", "Unsaved edits", MessageBoxButtons.YesNoCancel);
			switch (result)
			{
				case System.Windows.Forms.DialogResult.Cancel:
					return false;

				case System.Windows.Forms.DialogResult.No:
					return true;

				case System.Windows.Forms.DialogResult.Yes:
					SaveFile();
					return true;
			}
			return true;
		}

		private void exitToolStripMenuItem_Click(object sender, EventArgs e)
		{
			Application.Exit();
		}

		private void newToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (CurrentPipeline != null && !IsSaved && !ConfirmNoSave())
			{
				return;
			}
			m_CurrentFilepath = "";
			CreateNew();
		}

		private void openToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (CurrentPipeline != null && !IsSaved && !ConfirmNoSave())
			{
				return;
			}
			LoadFile();
		}

		private void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (CurrentPipeline != null)
			{
				m_CurrentFilepath = SaveFileAs();
			}
		}

		private void saveToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (CurrentPipeline != null)
			{
				SaveFile();
			}
		}

		#endregion Menu: File

		#region Menu: Simulation

		private void resetToolStripMenuItem_Click(object sender, EventArgs e)
		{
			ResetSimulation();
		}

		private void runToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (IsSimulationRunning())
			{
				StopSimulation();
			}
			else
			{
				StartSimulation();
			}
		}

		#endregion Menu: Simulation

		#region Menu: Tools

		private AssemblyForm m_AssemblyForm;
		private Form m_OptionsForm;
		private PipelineDataForm m_PipelineDataForm;

		public void HideDataWindow()
		{
			showDataWindowToolStripMenuItem.Text = "Show Data Window";
			if (m_PipelineDataForm == null || m_PipelineDataForm.IsDisposed)
			{
				return;
			}
			m_PipelineDataForm.Close();
		}

		public bool IsDataWindowVisible()
		{
			if (m_PipelineDataForm == null || m_PipelineDataForm.IsDisposed)
			{
				return false;
			}
			return m_PipelineDataForm.Visible;
		}

		public void ShowDataWindow()
		{
			if (m_PipelineDataForm == null || m_PipelineDataForm.IsDisposed)
			{
				m_PipelineDataForm = new PipelineDataForm(this);
				m_PipelineDataForm.FormClosed += m_PipelineDataForm_FormClosed;
			}
			m_PipelineDataForm.Show();
			m_PipelineDataForm.Location = RestoreBounds.Location;
			m_PipelineDataForm.Size = RestoreBounds.Size;
			m_PipelineDataForm.WindowState = WindowState;
			showDataWindowToolStripMenuItem.Text = "Hide Data Window";
		}

		private void assemblyInformationToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if ((m_AssemblyForm == null) || m_AssemblyForm.IsDisposed)
			{
				m_AssemblyForm = new AssemblyForm();
				m_AssemblyForm.Show();
				return;
			}
			if (m_AssemblyForm.Visible)
			{
				m_AssemblyForm.BringToFront();
			}
		}

		private void m_PipelineDataForm_FormClosed(object sender, FormClosedEventArgs e)
		{
			showDataWindowToolStripMenuItem.Text = "Show Data Window";
		}

		private void showDataWindowToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (IsDataWindowVisible())
			{
				HideDataWindow();
			}
			else
			{
				ShowDataWindow();
			}
		}

		private void optionsToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (m_OptionsForm == null || m_OptionsForm.IsDisposed)
			{
				m_OptionsForm = new Form
				{
					Controls =
					{
						new System.Windows.Forms.PropertyGrid
						{
							SelectedObject = Program.Config,
							Dock = DockStyle.Fill
						}
					},
					Size = new Size(470,550),
					Location = RestoreBounds.Location,
				};
				m_OptionsForm.Show();
			}
			m_OptionsForm.Focus();
		}

		#endregion Menu: Tools

		#endregion MenuStrip Implementation

		#region Windows Taskbar ThumbnailToolbar

		ThumbnailToolBarButton m_StartThumbnailButton;
		ThumbnailToolBarButton m_StopThumbnailButton;
		ThumbnailToolBarButton m_ResetThumbnailButton;

		private void InitializeThumbnail()
		{
			var manager = TaskbarManager.Instance;
			m_StartThumbnailButton = new ThumbnailToolBarButton(Icon.FromHandle(Properties.Resources.control_play.GetHicon()), "Start simulation");
			m_StopThumbnailButton = new ThumbnailToolBarButton(Icon.FromHandle(Properties.Resources.control_stop.GetHicon()), "Stop simulation");
			m_ResetThumbnailButton = new ThumbnailToolBarButton(Icon.FromHandle(Properties.Resources.control_start.GetHicon()), "Reset simulation");

			m_StartThumbnailButton.Click += delegate(object sender, ThumbnailButtonClickedEventArgs e)
			{
				if (!IsSimulationRunning())
				{
					StartSimulation();
				}
			};
			m_StopThumbnailButton.Click += delegate(object sender, ThumbnailButtonClickedEventArgs e)
			{
				StopSimulation();
			};
			m_ResetThumbnailButton.Click += delegate(object sender, ThumbnailButtonClickedEventArgs e)
			{
				ResetSimulation();
			};

			manager.ThumbnailToolBars.AddButtons(this.Handle,
				m_ResetThumbnailButton,
				m_StopThumbnailButton,
				m_StartThumbnailButton);
		}

		#endregion
	}
}