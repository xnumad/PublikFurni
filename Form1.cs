using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using Tangine;

using Sulakore.Habbo;
using Sulakore.Modules;
using Sulakore.Habbo.Web;
using Sulakore.Communication;
using Sulakore.Protocol;
using System.IO;
using Newtonsoft.Json;
using System.Dynamic;

namespace PublikFurni
{
    [Module("PublikFurni", "Room furni stealer!")]
    [Author("Alex", HabboName = "", Hotel = Sulakore.Habbo.HHotel.Com, ResourceName = "Follow on Twitter", ResourceUrl = "https://twitter.com/AmazingAussie")]

    public partial class Form1 : ExtensionForm
    {
        private ushort ObjectsMessageEvent;
        private ushort RoomDataMessageEvent;

        private int CurrentRoomId;
        private string CurrentRoomName;

        public Form1()
        {
            InitializeComponent();

            try
            {
                ObjectsMessageEvent = In.RoomFloorItems;
                RoomDataMessageEvent = In.RoomData;

                Triggers.InAttach(this.ObjectsMessageEvent, HandleObjectsMessageEvent);
                Triggers.InAttach(this.RoomDataMessageEvent, HandleRoomDataMessageEvent);
            }
            catch (Exception e)
            {
                MessageBox.Show(e.ToString());
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            Log("Public items stealer!");
        }

        /// <summary>
        /// Capture the room data to know which rooms to log.
        /// </summary>
        /// <param name="obj"></param>
        private void HandleRoomDataMessageEvent(DataInterceptedEventArgs obj)
        {
            obj.Packet.ReadBoolean();

            this.CurrentRoomId = obj.Packet.ReadInteger();
            this.CurrentRoomName = obj.Packet.ReadString();

            //Log("Room data ('" + CurrentRoomId + "', '" + this.CurrentRoomName + "')");
        }

        /// <summary>
        /// Will log the packet that sends all the floor item furniture and then finally saves it
        /// to JSON format using the room data logged in the packet above.
        /// </summary>
        /// <param name="obj"></param>
        private void HandleObjectsMessageEvent(DataInterceptedEventArgs obj)
        {
            dynamic itemData = new ExpandoObject();

            int owners = obj.Packet.ReadInteger();
            itemData.owners = new List<dynamic>();

            for (int i = 0; i < owners; i++) //in a group room, group admins count as "room owners" here
            {
                dynamic owner = new ExpandoObject();

                int ownerId = obj.Packet.ReadInteger();
                owner.ownerId = ownerId;

                string ownerName = obj.Packet.ReadString();
                owner.ownerName = ownerName;

                itemData.owners.Add(owner);
            }

            int items = obj.Packet.ReadInteger();
            itemData.items = new List<dynamic>();

            for (int i = 0; i < items; i++)
            {
                dynamic item = new ExpandoObject();

                int itemId = obj.Packet.ReadInteger();
                item.id = itemId;

                int spriteId = obj.Packet.ReadInteger();
                item.spriteId = spriteId;

                int x = obj.Packet.ReadInteger(); // X
                item.x = x;

                int y = obj.Packet.ReadInteger(); // Y
                item.y = y;

                int rotation = obj.Packet.ReadInteger(); // Rotation
                item.rotation = rotation;

                string height = obj.Packet.ReadString(); // Height
                item.height = height; //z position = furniture placement

                string sizeZ = obj.Packet.ReadString();
                item.sizeZ = sizeZ; //z dimensions = furniture height

                int extraDataPerspective = obj.Packet.ReadInteger();//_local_3._SafeStr_6897 = k._SafeStr_5432();
                item.extraDataVariable = extraDataPerspective;

                int extraDataType = obj.Packet.ReadInteger();//var _local_2:int = k._SafeStr_5432
                item.extraDataId = extraDataType;

                if (extraDataType == 0) // String
                {
                    string extraDataString = obj.Packet.ReadString();
                    item.extraDataString = extraDataString;
                }

                if (extraDataType == 2) // String array
                {
                    int strings = obj.Packet.ReadInteger();
                    item.strings = new List<String>();

                    for (int j = 0; j < strings; j++)
                    {
                        string str = obj.Packet.ReadString();
                        item.strings.Add(str);
                    }
                }

                if (extraDataType == 1) // Key value
                {
                    int strings = obj.Packet.ReadInteger();
                    item.keyValue = new List<String>();

                    for (int j = 0; j < strings; j++)
                    {
                        string key = obj.Packet.ReadString();
                        string value = obj.Packet.ReadString();

                        item.keyValue.Add(key);
                        item.keyValue.Add(value);
                    }
                }

                if (extraDataType == 5) // Integer array
                {
                    int integers = obj.Packet.ReadInteger();
                    item.ints = new List<int>();

                    for (int j = 0; j < integers; j++)
                    {
                        int number = obj.Packet.ReadInteger();
                        item.ints.Add(number);
                    }
                }

                // More junk
                obj.Packet.ReadInteger();
                obj.Packet.ReadInteger();
                obj.Packet.ReadInteger();

                itemData.items.Add(item);
            }

            var json = JsonConvert.SerializeObject(itemData);
            File.WriteAllText(Hotel.ToDomain() + this.CurrentRoomId + "-" + this.CurrentRoomName + ".json", json);
        }

        private void Log(Object text)
        {
            ThreadHelperClass.SetText(this, this.textBox1, this.textBox1.Text + " [" + DateTime.Now + "] " + text.ToString() + Environment.NewLine);
        }
    }

    public static class ThreadHelperClass
    {
        delegate void SetTextCallback(Form f, Control ctrl, string text);
        /// <summary>
        /// Set text property of various controls
        /// </summary>
        /// <param name="form">The calling form</param>
        /// <param name="ctrl"></param>
        /// <param name="text"></param>
        public static void SetText(Form form, Control ctrl, string text)
        {
            // InvokeRequired required compares the thread ID of the 
            // calling thread to the thread ID of the creating thread. 
            // If these threads are different, it returns true. 
            if (ctrl.InvokeRequired)
            {
                SetTextCallback d = new SetTextCallback(SetText);
                form.Invoke(d, new object[] { form, ctrl, text });
            }
            else
            {
                ctrl.Text = text;
            }
        }
    }
}
