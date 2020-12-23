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

            Log("Room data ('" + CurrentRoomId + "', '" + this.CurrentRoomName + "')");
        }

        /// <summary>
        /// Will log the packet that sends all the floor item furniture and then finally saves it
        /// to JSON format using the room data logged in the packet above.
        /// </summary>
        /// <param name="obj"></param>
        private void HandleObjectsMessageEvent(DataInterceptedEventArgs obj)
        {
            dynamic itemData = new ExpandoObject();
            Log("Got room objects packet");

            int owners = obj.Packet.ReadInteger();
            Log("Owners: " + owners);
            itemData.owners = new List<dynamic>();

            for (int i = 0; i < owners; i++) //in a group room, group admins count as "room owners" here
            {
                dynamic owner = new ExpandoObject();
                Log("Owner #" + i);

                int ownerId = obj.Packet.ReadInteger();
                Log("Owner #" + i + ".id=" + ownerId);
                owner.ownerId = ownerId;

                string ownerName = obj.Packet.ReadString();
                Log("Owner #" + i + ".name=" + ownerName);
                owner.ownerName = ownerName;

                itemData.owners.Add(owner);
            }

            int items = obj.Packet.ReadInteger();
            Log("Items: " + items);
            itemData.items = new List<dynamic>();

            for (int i = 0; i < items; i++)
            {
                dynamic item = new ExpandoObject();

                item.id = obj.Packet.ReadInteger();
                item.spriteId = obj.Packet.ReadInteger();
                item.x = obj.Packet.ReadInteger();
                item.y = obj.Packet.ReadInteger();
                item.dir = obj.Packet.ReadInteger();
                item.z = obj.Packet.ReadString(); //z position = furniture placement height
                item.sizeZ = obj.Packet.ReadString(); //z dimensions = furniture height
                item.extra = obj.Packet.ReadInteger();
                item.data = obj.Packet.ReadInteger();

                //4 hex nibbles store 2 values:
                //hex nibbles 1-2 for item.data //https://github.com/xnumad/HabboSwfOpenSource/blob/master/src/com/sulake/habbo/room/object/data/StuffDataFactory.as#L10
                //hex nibbles 3-4 for LTD
                //
                //Example:
                //   LTD extraDataType
                //0x 01  03

                bool LTD = false;
                if (item.data > 0xFF) //LTD indication
                {
                    LTD = true;
                    item.data &= 0xFF; //trim value for item.data
                }

                //https://github.com/JasonWibbo/HabboSwfOpenSource/tree/master/src/com/sulake/habbo/room/object/data
                //https://github.com/JasonWibbo/HabboSwfOpenSource/blob/master/src/com/sulake/habbo/room/object/data/StuffDataFactory.as
                //https://github.com/JasonWibbo/HabboSwfOpenSource/blob/master/src/com/sulake/habbo/room/IStuffData.as

                switch (item.data)
                {
                    case 0: // String //Legacy //https://github.com/JasonWibbo/HabboSwfOpenSource/blob/master/src/com/sulake/habbo/room/object/data/LegacyStuffData.as#L15
                        item.state = obj.Packet.ReadString();
                        break;
                    case 1: // Key value //Map //https://github.com/JasonWibbo/HabboSwfOpenSource/blob/master/src/com/sulake/habbo/room/object/data/MapStuffData.as#L28
                        int pairs = obj.Packet.ReadInteger();
                        item.keyValue = new Dictionary<string, string>();

                        for (int j = 0; j < pairs; j++)
                        {
                            item.keyValue.Add(obj.Packet.ReadString(), obj.Packet.ReadString());
                        }
                        break;
                    case 2: // String array //https://github.com/JasonWibbo/HabboSwfOpenSource/blob/master/src/com/sulake/habbo/room/object/data/StringArrayStuffData.as
                        int strings = obj.Packet.ReadInteger();
                        item.strings = new List<String>();

                        for (int j = 0; j < strings; j++)
                        {
                            item.strings.Add(obj.Packet.ReadString());
                        }
                        break;
                    case 3: //VoteResult //https://github.com/JasonWibbo/HabboSwfOpenSource/blob/master/src/com/sulake/habbo/room/object/data/VoteResultStuffData.as#L20
                        item.state = obj.Packet.ReadString();
                        item.result = obj.Packet.ReadInteger();
                        break;
                    case 4: //Empty //https://github.com/JasonWibbo/HabboSwfOpenSource/blob/master/src/com/sulake/habbo/room/object/data/EmptyStuffData.as#L10
                        break;
                    case 5: // Integer array //https://github.com/JasonWibbo/HabboSwfOpenSource/blob/master/src/com/sulake/habbo/room/object/data/IntArrayStuffData.as#L22
                        int integers = obj.Packet.ReadInteger();
                        item.ints = new List<int>();

                        for (int j = 0; j < integers; j++)
                        {
                            item.ints.Add(obj.Packet.ReadInteger());
                        }
                        break;
                    case 6: //high score //https://github.com/JasonWibbo/HabboSwfOpenSource/blob/master/src/com/sulake/habbo/room/object/data/HighScoreStuffData.as
                        item.state = obj.Packet.ReadString();
                        item.scoretype = obj.Packet.ReadInteger();
                        item.clearType = obj.Packet.ReadInteger();

                        int amountScores = obj.Packet.ReadInteger();
                        item.scores = new List<dynamic>();

                        for (int k = 0; k < amountScores; k++)
                        {
                            dynamic scoreData = new ExpandoObject();

                            scoreData.score = obj.Packet.ReadInteger();

                            int amountUsers = obj.Packet.ReadInteger();
                            scoreData.users = new List<String>();

                            for (int l = 0; l < amountUsers; l++)
                            {
                                scoreData.users.Add(obj.Packet.ReadString());
                            }

                            itemData.scores.Add(scoreData);
                        }
                        break;
                    case 7: //crackable
                        item.state = obj.Packet.ReadString();
                        item.hits = obj.Packet.ReadInteger();
                        item.target = obj.Packet.ReadInteger();
                        break;
                    default:
                        throw new NotImplementedException("item.data of item " + item.id + " is " + item.data + " and its interpretation is undefined and the packet can't be read any further from here on.");
                }

                if (LTD)
                {
                    item.uniqueSerialNumber = obj.Packet.ReadInteger();
                    item.uniqueSeriesSize = obj.Packet.ReadInteger();
                }

                item.rentTimeSecondsLeft = obj.Packet.ReadInteger(); //rent time in seconds, or -1
                //https://github.com/JasonWibbo/HabboSwfOpenSource/blob/master/src/com/sulake/habbo/ui/widget/infostand/InfoStandFurniView.as#L506
                //int infostand text           opens on click
                //<0  infostand.button.buy,    catalog page
                //>=0 infostand.button.buyout  buy window directly

                item.usagePolicy = obj.Packet.ReadInteger(); //https://github.com/JasonWibbo/HabboSwfOpenSource/blob/master/src/com/sulake/habbo/ui/widget/enums/RoomWidgetFurniInfoUsagePolicyEnum.as
                item.ownerId = obj.Packet.ReadInteger(); //Furniture owner ID

                itemData.items.Add(item);
            }

            Log("Finished with all items");
            var json = JsonConvert.SerializeObject(itemData);
            Log("Finished converting data to JSON");
            var path = Hotel.ToDomain() + this.CurrentRoomId + "-" + this.CurrentRoomName + ".json";

            Log($"Path: {path}");
            File.WriteAllText(MakeValidFileName(path), json); //fails if Tanji doesn't know the current hotel
            Log("Finished writing JSON file");
        }

        private void Log(Object text)
        {
            File.AppendAllText("publik-furni.log.txt", text.ToString() + Environment.NewLine);
            //ThreadHelperClass.SetText(this, this.textBox1, this.textBox1.Text + "[" + DateTime.Now + "] " + text.ToString() + Environment.NewLine);
        }

        private static string MakeValidFileName(string name) //https://stackoverflow.com/a/847251
        {
            string invalidChars = System.Text.RegularExpressions.Regex.Escape(new string(System.IO.Path.GetInvalidFileNameChars()));
            string invalidRegStr = string.Format(@"([{0}]*\.+$)|([{0}]+)", invalidChars);

            return System.Text.RegularExpressions.Regex.Replace(name, invalidRegStr, "_");
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
