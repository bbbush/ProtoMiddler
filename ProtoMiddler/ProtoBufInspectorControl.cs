﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Windows.Forms;
using ProtoMiddler.ProtoGen;

namespace ProtoMiddler
{
    public partial class ProtoBufInspectorControl : UserControl
    {
        readonly Dictionary<string, string> _lastUsedTypes = new Dictionary<string, string>();
        string MessageType;
        string ProtoFile;
        bool initialized;

        public ProtoBufInspectorControl()
        {
            InitializeComponent();
        }

        public byte[] ProtobufBytes { get; set; }

        public string Data
        {
            get { return rtbData.Text; }
            set { rtbData.Text = value; }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            InitializeLastUsedTypes();
        }

        public byte[] Encode()
        {
            if (string.IsNullOrEmpty(ProtoFile) || string.IsNullOrEmpty(MessageType))
            {
                return ProtobufBytes;
            }

            // try to encode using these things...

            return ProtoBufUtil.EncodeWithProto(Data, MessageType, ProtoFile);
        }

        void bnBrowse_Click(object sender, EventArgs e)
        {
            openFileDialog1.FileName = ProtoFile;
            if (DialogResult.OK == openFileDialog1.ShowDialog())
            {
                txtProtoFile.Text = openFileDialog1.FileName;

                ProtoFile = txtProtoFile.Text.Trim();
                UpdateTypeList();
            }
        }

        /// <summary>
        ///     parse the proto file to fill in the cbType combo box
        /// </summary>
        void UpdateTypeList()
        {
            cbType.Enabled = false;
            cbType.SelectedItem = null;
            cbType.Items.Clear();
            if (File.Exists(ProtoFile))
            {
                var list = new ProtoLoader(ProtoFile).LoadTypes()
                    .OrderByName(true);
                cbType.Items.AddRange(list.ToArray());
                cbType.Enabled = true;
                SetLastUsedType();
            }
        }

        void bnDecodeAs_Click(object sender, EventArgs e)
        {
            MessageType = (string) cbType.SelectedItem;
            ProtoFile = txtProtoFile.Text;

            if (!string.IsNullOrWhiteSpace(ProtoFile) &&
                !string.IsNullOrWhiteSpace(MessageType))
            {
                Data = ProtoBufUtil.DecodeWithProto(ProtobufBytes, MessageType, ProtoFile);
            }

            ProtoMiddlerState.Serialize(this);
        }

        void cbType_OnSelectedIndexChanged(object sender, EventArgs eventArgs)
        {
            var file = txtProtoFile.Text;
            var type = (string) cbType.SelectedItem;
            if (file != null)
            {
                var key = Path.GetFileName(file).Trim();
                if (!string.IsNullOrEmpty(key) &&
                    !string.IsNullOrWhiteSpace(type))
                {
                    _lastUsedTypes[key] = type.Trim();
                }
            }
        }

        void SetLastUsedType()
        {
            var file = txtProtoFile.Text;
            if (file != null)
            {
                var key = Path.GetFileName(file).Trim();
                if (_lastUsedTypes.ContainsKey(key))
                {
                    var value = _lastUsedTypes[key];
                    if (cbType.Items.Contains(value))
                    {
                        cbType.SelectedItem = value;
                    }
                }
            }
        }

        void InitializeLastUsedTypes()
        {
            if (initialized) return;
            try
            {
                ProtoMiddlerState.Deserialize(this);
                // cannot set values before OnLoad
                txtProtoFile.Text = ProtoFile;
                UpdateTypeList();
                Application.ApplicationExit += (sender, e) =>
                {
                    try
                    {
                        // cannot use values from UI while exiting
                        ProtoMiddlerState.Serialize(this);
                    }
                    catch
                    {
                    }
                };
            }
            catch
            {
            }
            initialized = true;
        }

        [DataContract]
        public class ProtoMiddlerState
        {
            const string TempFile = "ProtoMiddlerState.tmp";

            [DataMember]
            public Dictionary<string, string> LastUsedTypes { get; set; }

            [DataMember]
            public string LastUsedPath { get; set; }

            internal static void Deserialize(ProtoBufInspectorControl @this, string filename = TempFile)
            {
                var file = Path.Combine(Path.GetTempPath(), filename);
                if (File.Exists(file))
                {
                    using (var fs = File.OpenRead(file))
                    {
                        var ds = new DataContractSerializer(typeof (ProtoMiddlerState));
                        var state = ds.ReadObject(fs) as ProtoMiddlerState;
                        if (state != null)
                        {
                            @this.ProtoFile = state.LastUsedPath;
                            if (state.LastUsedTypes != null)
                            {
                                foreach (var pair in state.LastUsedTypes)
                                {
                                    if (!@this._lastUsedTypes.ContainsKey(pair.Key))
                                    {
                                        @this._lastUsedTypes[pair.Key] = pair.Value;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            internal static void Serialize(ProtoBufInspectorControl @this, string filename = TempFile)
            {
                var state = new ProtoMiddlerState
                {
                    LastUsedPath = @this.ProtoFile,
                    LastUsedTypes = @this._lastUsedTypes,
                };
                var file = Path.GetTempFileName();
                using (var fs = File.OpenWrite(file))
                {
                    var ds = new DataContractSerializer(typeof (ProtoMiddlerState));
                    ds.WriteObject(fs, state);
                }
                File.Replace(file, Path.Combine(Path.GetTempPath(), filename), null);
            }
        }
    }
}
