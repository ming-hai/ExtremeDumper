﻿using System;
using System.IO;
using System.Resources;
using System.Threading;
using System.Windows.Forms;
using dnlib.DotNet;
using FastWin32.Diagnostics;

namespace ExtremeDumper.Forms
{
    internal partial class InjectingForm : Form
    {
        private uint _processId;

        private string _assemblyPath;

        private ModuleDef _manifestModule;

        private MethodDef _entryPoint;

        private string _argument;

        private ResourceManager _resources = new ResourceManager(typeof(InjectingForm));

        public InjectingForm(uint processId, string processName)
        {
            InitializeComponent();
            Text = $"Injector - {processName}(ID={processId.ToString()})";
            _processId = processId;
        }

        #region Events
        private void InjectingForm_DragEnter(object sender, DragEventArgs e) => e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;

        private void InjectingForm_DragDrop(object sender, DragEventArgs e)
        {
            tbAssemblyPath.Text = ((string[])e.Data.GetData(DataFormats.FileDrop))[0];
            LoadAssembly();
        }

        private void InjectingForm_FormClosing(object sender, FormClosingEventArgs e) => _manifestModule.Dispose();

        private void tbAssemblyPath_TextChanged(object sender, EventArgs e) => _assemblyPath = tbAssemblyPath.Text;

        private void btSelectAssembly_Click(object sender, EventArgs e)
        {
            if (odlgSelectAssembly.ShowDialog() == DialogResult.OK)
                tbAssemblyPath.Text = odlgSelectAssembly.FileName;
            else
                return;
            LoadAssembly();
        }

        private void cmbEntryPoint_SelectedIndexChanged(object sender, EventArgs e) => _entryPoint = (MethodDef)cmbEntryPoint.SelectedItem;

        private void tbArgument_TextChanged(object sender, EventArgs e) => _argument = tbArgument.Text;

        private void btInject_Click(object sender, EventArgs e)
        {
            string typeName;

            if (!File.Exists(_assemblyPath))
                return;
            if (cmbEntryPoint.SelectedItem == null)
                return;
            typeName = _entryPoint.FullName.Substring(_entryPoint.FullName.IndexOf(' ') + 1);
            typeName = typeName.Substring(0, typeName.IndexOf(':'));
            if (chkWaitReturn.Checked)
            {
                btInject.Enabled = false;
                Text += _resources.GetString("StrWaiting");
                new Thread(() =>
                {
                    int ret;

                    if (Injector.InjectManaged(_processId, _assemblyPath, typeName, _entryPoint.Name, _argument, out ret))
                        Invoke((Action)(() => MessageBoxStub.Show($"{_resources.GetString("StrInjectSuccessfully")}\n{_resources.GetString("StrReturnValue")} {ret.ToString()}", MessageBoxIcon.Information)));
                    else
                        Invoke((Action)(() => MessageBoxStub.Show(_resources.GetString("StrFailToInject"), MessageBoxIcon.Error)));
                    Invoke((Action)(() =>
                    {
                        btInject.Enabled = true;
                        Text = Text.Substring(0, Text.Length - 6);
                    }));
                })
                {
                    IsBackground = true
                }.Start();
            }
            else
            {
                if (Injector.InjectManaged(_processId, _assemblyPath, typeName, _entryPoint.Name, _argument))
                    MessageBoxStub.Show(_resources.GetString("StrInjectSuccessfully"), MessageBoxIcon.Information);
                else
                    MessageBoxStub.Show(_resources.GetString("StrFailToInject"), MessageBoxIcon.Error);
            }
        }
        #endregion

        private void LoadAssembly()
        {
            MethodSig methodSig;

            try
            {
                _manifestModule = ModuleDefMD.Load(_assemblyPath);
            }
            catch
            {
                MessageBoxStub.Show(_resources.GetString("StrInvalidAssembly"), MessageBoxIcon.Error);
                _manifestModule = null;
                return;
            }
            cmbEntryPoint.Items.Clear();
            foreach (TypeDef typeDef in _manifestModule.GetTypes())
                foreach (MethodDef methodDef in typeDef.Methods)
                {
                    if (!methodDef.IsStatic)
                        continue;
                    if (methodDef.IsGetter || methodDef.IsSetter)
                        continue;
                    methodSig = (MethodSig)methodDef.Signature;
                    if (methodSig.Params.Count != 1 || methodSig.Params[0].FullName != "System.String")
                        continue;
                    if (methodSig.RetType.FullName != "System.Int32")
                        continue;
                    cmbEntryPoint.Items.Add(methodDef);
                }
        }
    }
}
