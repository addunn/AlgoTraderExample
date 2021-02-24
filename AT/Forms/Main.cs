using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Forms;
using AT.DataObjects;
using CefSharp;
using CefSharp.WinForms;
using MessagePack;

namespace AT
{
    public partial class Main : Form
    {
        public ChromiumWebBrowser browser;

        public Main()
        {
            InitializeComponent();

            App.Init();

            Cef.Initialize(new CefSettings());
            browser = new ChromiumWebBrowser("file:///" + Global.State.ExecutablePath + "html\\at\\index.html");
            this.Controls.Add(browser);
            browser.Dock = DockStyle.None;
            browser.JavascriptObjectRepository.Register("boundAsync", new BoundObject(), true);
            browser.LoadingStateChanged += OnLoadingStateChanged;

        }
        private void OnLoadingStateChanged(object sender, LoadingStateChangedEventArgs args)
        {
            if (!args.IsLoading)
            {
                // page has finished loading...
                browser.ShowDevTools();
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            resizeBrowser();
            
        }
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            Cef.Shutdown();
        }
        private void Form1_Resize(object sender, EventArgs e)
        {
            resizeBrowser();
        }
        private void resizeBrowser()
        {
            browser.Width = this.DisplayRectangle.Width;
            browser.Height = this.DisplayRectangle.Height;
            browser.Top = 0;
            browser.Left = 0;
        }
        private void Form1_ResizeEnd(object sender, EventArgs e)
        {
            resizeBrowser();
            //browser.Visible = true;
        }
        private void Form1_SizeChanged(object sender, EventArgs e)
        {
            resizeBrowser();
        }

        private void MainTimer_Tick(object sender, EventArgs e)
        {
            // AlgoTrader at = Data.AlgoTrader;


            string payload = App.GetSubscribedStateChanges();

            if (!string.IsNullOrWhiteSpace(payload))
            {
                browser.ExecuteScriptAsync("app.receive(" + payload + ")");
            }
            // log sending
            /*
            if (at != null && State.SendLog && at.Log.ChangedSinceLastRead(Log.Verbosity.Verbose))
            {
                string escapedString = HttpUtility.JavaScriptStringEncode(at.Log.Read(Log.Verbosity.Verbose));

                browser.ExecuteScriptAsync("app.formReceive.refreshAlgoTraderLog(\"" + escapedString + "\")");
            }
            */

            /*
            if (at != null && State.LastCanBeShutDown != at.CanBeShutDown)
            {
                State.LastCanBeShutDown = at.CanBeShutDown;
                browser.ExecuteScriptAsync("app.formReceive.toggleAlgoTraderCanBeShutdown(" + UC.BoolToJSBool(State.LastCanBeShutDown) + ")");
            }
            */
            /*
            project.ProjectItemsRankingChangeIndicator = project.ProjectItemsRankingLastChangeIndicator;
            string json = Serializer.JSONSerializeProjectItems(project, n, true);
            string escapedJson = HttpUtility.JavaScriptStringEncode(json);
            browser.ExecuteScriptAsync("app.formReceive.networkListRefresh(\"" + escapedJson + "\")");
            */

        }

        private void CommandWatcher_Tick(object sender, EventArgs e)
        {
            if(Global.State.Commands.Count > 0)
            {
                // take the first one
                string cmd = Global.State.Commands[0];
                string[] cv = cmd.Split(':');
                switch (cv[0])
                {
                    case "log":
                        if(cv[1] == "on")
                        {

                            Global.State.SendLog = true;
                        }
                        else if(cv[1] == "off")
                        {
                            Global.State.SendLog = false;
                        }
                        break;
                    
                    case "change-timer-speed":
                        break;
                    case "unsubscribe":
                        for(int n = 1; n < cv.Length; n++)
                        {
                            Global.State.Subscriptions[cv[n]].Subscribed = false;
                        }
                        break;
                    case "set-selected-thread-control-id":
                        Global.State.SelectedThreadControlId = int.Parse(cv[1]);
                        Global.State.ThreadControlIdChanged = true;
                        break;
                    case "subscribe":
                        for (int n = 1; n < cv.Length; n++)
                        {
                            Global.State.Subscriptions[cv[n]].Subscribed = true;
                        }
                        break;
                    case "algo-subscribe":
                        for (int n = 1; n < cv.Length; n++)
                        {
                            Global.State.AlgoTraderSubscriptions[cv[n]] = true;
                        }
                        AlgoTraderUI.IsSubscribed = true;
                        break;
                    case "algo-clear-selected-symbol":
                        AlgoTraderUI.SelectedSymbol = "";
                        break;
                    case "algo-clear-selected-strategy-id":
                        AlgoTraderUI.SelectedStrategyId = "";
                        break;
                    case "algo-set-selected-strategy-id":
                        AlgoTraderUI.SelectedStrategyId = cv[1];
                        break;
                    case "algo-set-selected-symbol":
                        AlgoTraderUI.SelectedSymbol = cv[1];
                        break;
                    case "algo-set-speed":
                        AlgoTraderMethods.SetSpeed((AlgoTraderEnums.AlgoTraderSimulatingSpeed)int.Parse(cv[1]));
                        break;
                    case "algo-unsubscribe-all":
                        AlgoTraderUI.UnsubscribeToAll();
                        break;
                    case "algo-unsubscribe":
                        for (int n = 1; n < cv.Length; n++)
                        {
                            Global.State.AlgoTraderSubscriptions[cv[n]] = false;
                        }
                        if (AlgoTraderUI.IsUnsubscribedToAll())
                        {
                            AlgoTraderUI.IsSubscribed = false;
                        }
                        break;
                    case "graph-set-dimensions":
                        AlgoTraderUI.GraphCanvasWidth = int.Parse(cv[1]);
                        AlgoTraderUI.GraphCanvasHeight = int.Parse(cv[2]);
                        break;
                    case "start-scheduler":
                        App.StartAppScheduler();
                        break;
                    case "set-data-table-sort":
                        // set-data-table-sort:overview-symbols:4:asc
                        //set-data-table-sort:" + dataTableName + ":" + index + ":" + dirLabel);
                        //set-data-table-sort:overview-symbols:0:asc
                        string tableName = cv[1];
                        string sortColumn = cv[2];
                        string sortDir = cv[3];

                        if (!AlgoTraderUI.DataTableSorts.ContainsKey(tableName))
                        {
                            AlgoTraderUI.DataTableSorts.Add(tableName, new AlgoTraderObjects.DataTableSort());
                        }
                        AlgoTraderUI.DataTableSorts[tableName].Direction = sortDir;
                        AlgoTraderUI.DataTableSorts[tableName].Column = sortColumn;

                        break;
                    case "stop-scheduler":

                        //string s = Tools.SerializerMethods.SerializeThreadControlTree();
                        //bool breakme = true;
                        App.StopAppScheduler();
                        break;
                    default:
                        break;
                }

                // remove that item (will always be the first element)
                Global.State.Commands.Remove(cmd);
            }
        }

        private void ApplicationStats_Tick(object sender, EventArgs e)
        {

        }

        private void AlgoTraderSubscriptions_Tick(object sender, EventArgs e)
        {
            lock (AlgoTraderUI.LockObj)
            {

                if (AlgoTraderUI.Payload != null)
                {
                    browser.ExecuteScriptAsync("app.receive(" + AlgoTraderUI.Payload + ")");
                    AlgoTraderUI.Payload = null;
                }
            }
        }
    }

    // declare the new class
    public class BoundObject
    {
        // declare the procedure called by JS
        // in this example a value passed from JS is assigned to a TextBox1 on Form1
        public void uiCommand(string cmd)
        {
            Global.State.Commands.Add(cmd);
        }
    }

}
