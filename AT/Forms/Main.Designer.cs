namespace AT
{
    partial class Main
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.mainTimer = new System.Windows.Forms.Timer(this.components);
            this.commandWatcher = new System.Windows.Forms.Timer(this.components);
            this.ApplicationStats = new System.Windows.Forms.Timer(this.components);
            this.AlgoTraderSubscriptions = new System.Windows.Forms.Timer(this.components);
            this.SuspendLayout();
            // 
            // mainTimer
            // 
            this.mainTimer.Enabled = true;
            this.mainTimer.Interval = 500;
            this.mainTimer.Tick += new System.EventHandler(this.MainTimer_Tick);
            // 
            // commandWatcher
            // 
            this.commandWatcher.Enabled = true;
            this.commandWatcher.Interval = 20;
            this.commandWatcher.Tick += new System.EventHandler(this.CommandWatcher_Tick);
            // 
            // ApplicationStats
            // 
            this.ApplicationStats.Enabled = true;
            this.ApplicationStats.Interval = 1500;
            this.ApplicationStats.Tick += new System.EventHandler(this.ApplicationStats_Tick);
            // 
            // AlgoTraderSubscriptions
            // 
            this.AlgoTraderSubscriptions.Enabled = true;
            this.AlgoTraderSubscriptions.Tick += new System.EventHandler(this.AlgoTraderSubscriptions_Tick);
            // 
            // Main
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 19F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.Black;
            this.ClientSize = new System.Drawing.Size(854, 625);
            this.Font = new System.Drawing.Font("Bahnschrift SemiCondensed", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.Name = "Main";
            this.ShowIcon = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Algo Trader";
            this.Load += new System.EventHandler(this.Form1_Load);
            this.ResizeEnd += new System.EventHandler(this.Form1_ResizeEnd);
            this.SizeChanged += new System.EventHandler(this.Form1_SizeChanged);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Timer mainTimer;
        private System.Windows.Forms.Timer commandWatcher;
        private System.Windows.Forms.Timer ApplicationStats;
        private System.Windows.Forms.Timer AlgoTraderSubscriptions;
    }
}

