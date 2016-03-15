﻿using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.IO;
using System.Linq;
using OpcMock.Properties;

namespace OpcMock
{
    public partial class DemoForm : Form
    {
        

        private string dataFilePath;
        private string projectFilePath;
        private string protocolFilePath;
        private OpcMockProject opcMockProject;
        private ProjectFileWriter projectFileWriter;

        private OpcReader opcReader;
        private OpcWriter opcWriter;

        private int currentProtocolLine;

        public DemoForm()
        {
            InitializeComponent();

            SetDgvPropertiesThatTheDesignerKeepsLosing();

            InitializeMembers();
        }

        private void SetDgvPropertiesThatTheDesignerKeepsLosing()
        {
            TagQualityText.DataSource = Enum.GetNames(typeof(OpcTag.OpcTagQuality));
            dgvOpcData.CurrentCellDirtyStateChanged += dgvOpcData_CurrentCellDirtyStateChanged;
        }

        private void InitializeMembers()
        {
            dataFilePath = string.Empty;

            sfdProjectFile.Filter = @"OPC Mock Project|*" + FileExtensionContants.FileExtensionProject;

            currentProtocolLine = 0;
        }

        private void btnReadOpcData_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(dataFilePath)) return;

            FillOpcDataGrid(opcReader.ReadAllTags());
        }

        private void EnableButtonsAfterDataFileLoad()
        {
            btnSaveTags.Enabled = true;
            btnReadTags.Enabled = true;
            btnStep.Enabled = true;
        }

        private void FillOpcDataGrid(List<OpcTag> opcTags)
        {
            dgvOpcData.Rows.Clear();

            foreach (OpcTag opcTag in opcTags)
            {
                int newRowIndex = dgvOpcData.Rows.Add();

                dgvOpcData.Rows[newRowIndex].Cells[0].Value = opcTag.Path;
                dgvOpcData.Rows[newRowIndex].Cells[1].Value = opcTag.Value;
                dgvOpcData.Rows[newRowIndex].Cells[2].Value = opcTag.Quality.ToString();
                dgvOpcData.Rows[newRowIndex].Cells[3].Value = ((int)opcTag.Quality).ToString();
            }

            //Avoid first data cell starting as "Edit" and therefore being cleared
            dgvOpcData.CurrentCell = dgvOpcData.Rows[dgvOpcData.RowCount - 1].Cells[0];
        }

        private void btnSaveData_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(dataFilePath))
            {
                MessageBox.Show(Resources.DemoForm_btnSaveData_Click_Set_target_file_tagPath_);
                
                return;
            }

            WriteDataToFile();
        }

        private void WriteDataToFile()
        {
            if (dgvOpcData.Rows.Count <= 0) return;
            List<OpcTag> tagDataFromDgv = GenerateTagListFromDataGridView();

            opcWriter.WriteAllTags(tagDataFromDgv);
        }

        private List<OpcTag> GenerateTagListFromDataGridView()
        {
            return (from DataGridViewRow dgvr in dgvOpcData.Rows
                    where (dgvr.Index < dgvOpcData.Rows.Count - 1
                             && dgvr.Cells[0].Value != null
                             && dgvr.Cells[1].Value != null)
                    let qualityFromInt = (OpcTag.OpcTagQuality)Convert.ToInt32(dgvr.Cells[3].FormattedValue)
                    select new OpcTag(dgvr.Cells[0].Value.ToString(), dgvr.Cells[1].Value.ToString(), qualityFromInt)).ToList();
        }

        void dgvOpcData_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            DataGridViewCell currentCell = dgvOpcData.CurrentCell;

            dgvOpcData.CommitEdit(new DataGridViewDataErrorContexts());

            if (currentCell.OwningColumn.Name.Equals("TagQualityText"))
            {
                string workingValue = currentCell.Value?.ToString() ?? OpcTag.OpcTagQuality.Good.ToString();

                dgvOpcData.Rows[currentCell.RowIndex].Cells["TagQualityValue"].Value = ((int)Enum.Parse(typeof(OpcTag.OpcTagQuality), workingValue)).ToString();
            }
        }

        private void btnStep_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(dataFilePath)) return;

            ExecuteProtocolLine();
        }

        private static bool IsEndOfProtocol(string lineToExecute)
        {
            return string.IsNullOrWhiteSpace(lineToExecute);
        }

        private void ExecuteProtocolLine() 
        {
            string lineToExecute = rtbProtocol.Lines[currentProtocolLine];

            try
            {
                ProtocolLine protocolLine = new ProtocolLine(lineToExecute);

                if (protocolLine.Action.Equals(ProtocolLine.Actions.Set))
                {
                    SetSingleTagFromProtocol(protocolLine);
                }
                else if (protocolLine.Action.Equals(ProtocolLine.Actions.Wait))
                {
                    CheckExpectedTagFromProtocol(protocolLine);
                }
                else if (protocolLine.Action.Equals(ProtocolLine.Actions.Dummy))
                {
                    IncrementCurrentProtocolLine();
                }
            }
            catch (ProtocolActionException)
            {
                MessageBox.Show("Invalid protocol action for line: " + lineToExecute);
            }
        }

        private void SetSingleTagFromProtocol(ProtocolLine protocolLine)
        {
            opcWriter.WriteSingleTag(new OpcTag(protocolLine.TagPath, protocolLine.TagValue, OpcTagQualityFromInteger(protocolLine.TagQualityInt)));

            IncrementCurrentProtocolLine();

            FillOpcDataGrid(opcReader.ReadAllTags());
        }

        private OpcTag.OpcTagQuality OpcTagQualityFromInteger(string qualityAsString)
        {
            return (OpcTag.OpcTagQuality)Convert.ToInt32(qualityAsString);
        }

        private void CheckExpectedTagFromProtocol(ProtocolLine protocolLine)
        {
            List<OpcTag> opcTagList = opcReader.ReadAllTags();

            OpcTag.OpcTagQuality qualityFromInt =
                (OpcTag.OpcTagQuality)Convert.ToInt32(protocolLine.TagQualityInt);
            OpcTag tagToCheck = new OpcTag(protocolLine.TagPath, protocolLine.TagValue, qualityFromInt);

            if (opcTagList.Contains(tagToCheck))
            {
                FillOpcDataGrid(opcTagList);
                IncrementCurrentProtocolLine();
            }
        }

        private void IncrementCurrentProtocolLine()
        {
            currentProtocolLine++;

            if (IsEndOfProtocol(rtbProtocol.Lines[currentProtocolLine]))
            {
                btnStep.Text = "Done";
                btnStep.Enabled = false;
                btnResetProtocol.Enabled = true;
            }
            else
            {
                btnStep.Text = "Execute step " + (currentProtocolLine + 1);
            }
        }

        private void btnResetProtocol_Click(object sender, EventArgs e)
        {
            currentProtocolLine = -1;
            btnResetProtocol.Enabled = false;

            IncrementCurrentProtocolLine();
            btnStep.Enabled = true;
        }

        #region File menu handlers

        private void newToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CreateProjectDialog cpd = new CreateProjectDialog();
            cpd.StartPosition = FormStartPosition.CenterParent;

            if (DialogResult.OK.Equals(cpd.ShowDialog(this)))
            {
                projectFileWriter = new ProjectFileWriter(cpd.ProjectFolderPath, cpd.Project.Name);
                opcMockProject = cpd.Project;
                projectFileWriter.SaveProjectFileContent();
                dataFilePath = CreateDataFilePath();

                if (!File.Exists(dataFilePath))
                {
                    File.Create(dataFilePath).Close();
                }

                opcReader = new OpcReaderCsv(dataFilePath);
                opcWriter = new OpcWriterCsv(dataFilePath);

                this.Text = "OPC Mock - " + opcMockProject.Name;
                EnableButtonsAfterDataFileLoad();
            }

            cpd.Dispose();
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (DialogResult.OK.Equals(ofdProjectFile.ShowDialog(this)))
            {
                projectFilePath = ofdProjectFile.FileName;
                
                ProjectFileReader pfr = new ProjectFileReader(projectFilePath);

                opcMockProject = pfr.OpcMockProject;

                dataFilePath = projectFilePath.Replace(OpcMockConstants.FileExtensionProject, OpcMockConstants.FileExtensionData);

                projectFileWriter = new ProjectFileWriter(Path.GetDirectoryName(projectFilePath), pfr.OpcMockProject.Name);

                if (!File.Exists(dataFilePath))
                {
                    File.Create(dataFilePath).Close();
                }

                opcReader = new OpcReaderCsv(dataFilePath);
                opcWriter = new OpcWriterCsv(dataFilePath);

                FillOpcDataGrid(opcReader.ReadAllTags());

                this.Text = "OPC Mock - " + opcMockProject.Name;
                EnableButtonsAfterDataFileLoad();
            }
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            projectFileWriter.SaveProjectFileContent();
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        #endregion

        private string CreateDataFilePath()
        {
            return projectFileWriter.FolderPath + Path.DirectorySeparatorChar + opcMockProject.Name + FileExtensionContants.FileExtensionData;
        }

        private string GetProjectFolderPath()
        {
            return Path.GetDirectoryName(projectFilePath);
        }

        private void newToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            CreateProtocolDialog cpd = new CreateProtocolDialog();
            cpd.StartPosition = FormStartPosition.CenterParent;

            if (DialogResult.OK.Equals(cpd.ShowDialog(this)))
            {
                try
                {
                    opcMockProject.AddProtocol(cpd.OpcMockProtocol);
                }
                catch (DuplicateProtocolNameException exProtocolName)
                {
                    MessageBox.Show(this, exProtocolName.Message + "\n" + exProtocolName.ProtocolName);
                }
            }

            cpd.Dispose();
        }
    }
}
