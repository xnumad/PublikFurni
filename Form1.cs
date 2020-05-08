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
                Log("Item #" + i);

                int itemId = obj.Packet.ReadInteger();
                Log("Item #" + i + ".id=" + itemId);
                item.id = itemId;

                int spriteId = obj.Packet.ReadInteger();
                Log("Item #" + i + ".spriteId=" + spriteId);
                item.spriteId = spriteId;

                int x = obj.Packet.ReadInteger(); // X
                Log("Item #" + i + ".x=" + x);
                item.x = x;

                int y = obj.Packet.ReadInteger(); // Y
                Log("Item #" + i + ".y=" + y);
                item.y = y;

                int rotation = obj.Packet.ReadInteger(); // Rotation //Direction
                Log("Item #" + i + ".rotation=" + rotation);
                item.rotation = rotation;

                string height = obj.Packet.ReadString(); // Height
                Log("Item #" + i + ".height=" + height);
                item.height = height; //z position = furniture placement

                string sizeZ = obj.Packet.ReadString();
                Log("Item #" + i + ".sizeZ=" + sizeZ);
                item.sizeZ = sizeZ; //z dimensions = furniture height

                int extraDataPerspective = obj.Packet.ReadInteger();//_local_3._SafeStr_6897 = k._SafeStr_5432();
                Log("Item #" + i + ".extraDataPerspective=" + extraDataPerspective);
                item.extraDataVariable = extraDataPerspective;

                int extraDataType = obj.Packet.ReadInteger();//var _local_2:int = k._SafeStr_5432
                Log("Item #" + i + ".extraDataType=" + extraDataType);
                item.extraDataId = extraDataType;

                //https://github.com/JasonWibbo/HabboSwfOpenSource/tree/master/src/com/sulake/habbo/room/object/data
                //https://github.com/JasonWibbo/HabboSwfOpenSource/blob/master/src/com/sulake/habbo/room/object/data/StuffDataFactory.as
                //https://github.com/JasonWibbo/HabboSwfOpenSource/blob/master/src/com/sulake/habbo/room/IStuffData.as

                if (extraDataType == 0) // String //Legacy //https://github.com/JasonWibbo/HabboSwfOpenSource/blob/master/src/com/sulake/habbo/room/object/data/LegacyStuffData.as#L15
                {
                    string extraDataString = obj.Packet.ReadString();
                    Log("Item #" + i + ".extraDataString=" + extraDataString);
                    item.extraDataString = extraDataString;
                }

                else if (extraDataType == 1) // Key value //Map //https://github.com/JasonWibbo/HabboSwfOpenSource/blob/master/src/com/sulake/habbo/room/object/data/MapStuffData.as#L28
                {
                    int strings = obj.Packet.ReadInteger();
                    Log("Item #" + i + ".strings[].length=" + strings);
                    item.keyValue = new Dictionary<string, string>();

                    for (int j = 0; j < strings; j++)
                    {
                        string key = obj.Packet.ReadString();
                        Log("Item #" + i + ".strings[" + j + "].key=" + key);
                        string value = obj.Packet.ReadString();
                        Log("Item #" + i + ".strings[" + j + "].value=" + value);

                        item.keyValue.Add(key, value);
                    }
                }

                else if (extraDataType == 2) // String array //https://github.com/JasonWibbo/HabboSwfOpenSource/blob/master/src/com/sulake/habbo/room/object/data/StringArrayStuffData.as
                {
                    int strings = obj.Packet.ReadInteger();
                    item.strings = new List<String>();
                    Log("Item #" + i + ".strings[].length=" + strings);

                    for (int j = 0; j < strings; j++)
                    {
                        string str = obj.Packet.ReadString();
                        Log("Item #" + i + ".strings[" + j + "]=" + str);
                        item.strings.Add(str);
                    }
                }

                else if (extraDataType == 3) //VoteResult //https://github.com/JasonWibbo/HabboSwfOpenSource/blob/master/src/com/sulake/habbo/room/object/data/VoteResultStuffData.as#L20
                {
                    string state = obj.Packet.ReadString();
                    Log("Item #" + i + ".state=" + state);
                    item.state = state;

                    int result = obj.Packet.ReadInteger();
                    Log("Item #" + i + ".result=" + result);
                    item.result = result;
                }

                else if (extraDataType == 4) { } //Empty //https://github.com/JasonWibbo/HabboSwfOpenSource/blob/master/src/com/sulake/habbo/room/object/data/EmptyStuffData.as#L10

                else if (extraDataType == 5) // Integer array //https://github.com/JasonWibbo/HabboSwfOpenSource/blob/master/src/com/sulake/habbo/room/object/data/IntArrayStuffData.as#L22
                {
                    int integers = obj.Packet.ReadInteger();
                    Log("Item #" + i + ".integers[].length=" + integers);
                    item.ints = new List<int>();

                    for (int j = 0; j < integers; j++)
                    {
                        int number = obj.Packet.ReadInteger();
                        Log("Item #" + i + ".integers[" + j + "]=" + number);
                        item.ints.Add(number);
                    }
                }

                else if (extraDataType == 6) //high score //https://github.com/JasonWibbo/HabboSwfOpenSource/blob/master/src/com/sulake/habbo/room/object/data/HighScoreStuffData.as
                {
                    item.state = obj.Packet.ReadString();
                    item.scoretype = obj.Packet.ReadInteger();
                    item.clearType = obj.Packet.ReadInteger();

                    int amountScores = obj.Packet.ReadInteger();
                    Log("Item #" + i + ".scores[].length=" + amountScores);
                    item.scores = new List<dynamic>();

                    for (int k = 0; k < amountScores; k++)
                    {
                        dynamic scoreData = new ExpandoObject();
                        Log("Score #" + k);

                        scoreData.score = obj.Packet.ReadInteger();
                        Log("Score #" + k + ".score=" + scoreData.score);

                        int amountUsers = obj.Packet.ReadInteger();
                        Log("Score #" + k + ".players=" + amountUsers);
                        scoreData.users = new List<String>();

                        for (int l = 0; l < amountUsers; l++)
                        {
                            string username = obj.Packet.ReadString();
                            Log("Score #" + k + ".players[" + l + "]=" + username);
                            scoreData.users.Add(username);
                        }
                        
                        itemData.scores.Add(scoreData);
                    }
                }

                else if (extraDataType == 7) //crackable
                {
                    item.state = obj.Packet.ReadString();
                    Log("Item #" + i + ".state=" + item.state);

                    item.hits = obj.Packet.ReadInteger();
                    Log("Item #" + i + ".hits=" + item.hits);

                    item.target = obj.Packet.ReadInteger();
                    Log("Item #" + i + ".target=" + item.target);
                }

                else MessageBox.Show("Sorry, extraDataType of item " + itemId + " is " + extraDataType + " and its interpretation is undefined.");

                // More junk
                int rentTimeSecondsLeft = obj.Packet.ReadInteger(); //rent time in seconds, or -1
                item.rentTimeSecondsLeft = rentTimeSecondsLeft;
                Log("Item #" + i + ".rentTimeSecondsLeft=" +rentTimeSecondsLeft);
                //https://github.com/JasonWibbo/HabboSwfOpenSource/blob/master/src/com/sulake/habbo/ui/widget/enums/RoomWidgetFurniInfoUsagePolicyEnum.as
                //https://github.com/JasonWibbo/HabboSwfOpenSource/blob/master/src/com/sulake/habbo/ui/widget/infostand/InfoStandFurniView.as#L506
                //int infostand text           opens on click
                //<0  infostand.button.buy,    catalog page
                //>=0 infostand.button.buyout  buy window directly

                int amountOfStates = obj.Packet.ReadInteger(); //amount of states furni has. infostand shows use button in info, if 1 or 2
                item.amountOfStates = amountOfStates;
                Log("Item #" + i + ".amountOfStates=" + amountOfStates);

                int ownerId = obj.Packet.ReadInteger();
                Log("Item #" + i + ".ownerId=" + ownerId);
                item.ownerId = ownerId; //Furniture owner ID

                itemData.items.Add(item);
            }

            Log("Finished with all items");
            var json = JsonConvert.SerializeObject(itemData);
            Log("Finished converting data to JSON");
            File.WriteAllText(/*Hotel.ToDomain() + */this.CurrentRoomId + "-" + this.CurrentRoomName + ".json", json); //fails if Tanji doesn't know the current hotel
            Log("Finished writing JSON file");
        }

        private void Log(Object text)
        {
            File.AppendAllText("publik-furni.log.txt", text.ToString() + Environment.NewLine);
            //ThreadHelperClass.SetText(this, this.textBox1, this.textBox1.Text + "[" + DateTime.Now + "] " + text.ToString() + Environment.NewLine);
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
