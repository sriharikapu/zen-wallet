
// This file has been generated by the GUI designer. Do not modify.
namespace Wallet
{
	public partial class SendDialogStep1
	{
		private global::Gtk.VBox vbox2;

		private global::Wallet.DialogField dialogfieldTo;

		private global::Wallet.DialogField dialogfieldAmount;

		private global::Gtk.Alignment alignment1;

		private global::Gtk.HBox hbox3;

		private global::Gtk.EventBox eventboxSend;

		private global::Gtk.Image image1;

		private global::Gtk.Button buttonRaw;

		protected virtual void Build()
		{
			global::Stetic.Gui.Initialize(this);
			// Widget Wallet.SendDialogStep1
			global::Stetic.BinContainer.Attach(this);
			this.Name = "Wallet.SendDialogStep1";
			// Container child Wallet.SendDialogStep1.Gtk.Container+ContainerChild
			this.vbox2 = new global::Gtk.VBox();
			this.vbox2.Name = "vbox2";
			this.vbox2.Spacing = 6;
			// Container child vbox2.Gtk.Box+BoxChild
			this.dialogfieldTo = new global::Wallet.DialogField();
			this.dialogfieldTo.Events = ((global::Gdk.EventMask)(256));
			this.dialogfieldTo.Name = "dialogfieldTo";
			this.dialogfieldTo.Value = "";
			this.vbox2.Add(this.dialogfieldTo);
			global::Gtk.Box.BoxChild w1 = ((global::Gtk.Box.BoxChild)(this.vbox2[this.dialogfieldTo]));
			w1.Position = 0;
			w1.Expand = false;
			w1.Fill = false;
			// Container child vbox2.Gtk.Box+BoxChild
			this.dialogfieldAmount = new global::Wallet.DialogField();
			this.dialogfieldAmount.Events = ((global::Gdk.EventMask)(256));
			this.dialogfieldAmount.Name = "dialogfieldAmount";
			this.vbox2.Add(this.dialogfieldAmount);
			global::Gtk.Box.BoxChild w2 = ((global::Gtk.Box.BoxChild)(this.vbox2[this.dialogfieldAmount]));
			w2.Position = 1;
			w2.Expand = false;
			w2.Fill = false;
			// Container child vbox2.Gtk.Box+BoxChild
			this.alignment1 = new global::Gtk.Alignment(0.5F, 0.5F, 1F, 1F);
			this.alignment1.HeightRequest = 10;
			this.alignment1.Name = "alignment1";
			this.vbox2.Add(this.alignment1);
			global::Gtk.Box.BoxChild w3 = ((global::Gtk.Box.BoxChild)(this.vbox2[this.alignment1]));
			w3.Position = 2;
			w3.Expand = false;
			// Container child vbox2.Gtk.Box+BoxChild
			this.hbox3 = new global::Gtk.HBox();
			this.hbox3.Name = "hbox3";
			this.hbox3.Homogeneous = true;
			this.hbox3.Spacing = 6;
			// Container child hbox3.Gtk.Box+BoxChild
			this.eventboxSend = new global::Gtk.EventBox();
			this.eventboxSend.Name = "eventboxSend";
			this.eventboxSend.VisibleWindow = false;
			// Container child eventboxSend.Gtk.Container+ContainerChild
			this.image1 = new global::Gtk.Image();
			this.image1.Name = "image1";
			this.image1.Pixbuf = global::Gdk.Pixbuf.LoadFromResource("Wallet.Assets.misc.send_dialog.png");
			this.eventboxSend.Add(this.image1);
			this.hbox3.Add(this.eventboxSend);
			global::Gtk.Box.BoxChild w5 = ((global::Gtk.Box.BoxChild)(this.hbox3[this.eventboxSend]));
			w5.Position = 1;
			w5.Expand = false;
			w5.Fill = false;
			// Container child hbox3.Gtk.Box+BoxChild
			this.buttonRaw = new global::Gtk.Button();
			this.buttonRaw.CanFocus = true;
			this.buttonRaw.Name = "buttonRaw";
			this.buttonRaw.UseUnderline = true;
			this.buttonRaw.Label = global::Mono.Unix.Catalog.GetString("Raw");
			this.hbox3.Add(this.buttonRaw);
			global::Gtk.Box.BoxChild w6 = ((global::Gtk.Box.BoxChild)(this.hbox3[this.buttonRaw]));
			w6.Position = 2;
			w6.Expand = false;
			w6.Fill = false;
			this.vbox2.Add(this.hbox3);
			global::Gtk.Box.BoxChild w7 = ((global::Gtk.Box.BoxChild)(this.vbox2[this.hbox3]));
			w7.Position = 3;
			w7.Expand = false;
			w7.Fill = false;
			this.Add(this.vbox2);
			if ((this.Child != null))
			{
				this.Child.ShowAll();
			}
			this.Hide();
		}
	}
}
