using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Orderbook
{
    public partial class Form1 : Form
    {
        enum OrderSide
        {
            Buy = 1,
            Sell = -1
        }

        enum OrderStatus
        {
            Working = 1,
            Filled = 2,
            Canceled = 3
        }

        private class OrderUpdate
        {
            public int OrderID { get; set; }
            public OrderSide Side { get; set; }
            public double Price { get; set; }
            public int Quantity { get; set; }
            public OrderStatus Status { get; set; }
        }
        //dtTemp用于保存在暂停情况时，从文件读取到的数据
        private DataTable dtTemp = new DataTable();
        //委托，用于界面线程安全
        private delegate void updateDelegate(OrderUpdate update);
        //标记是否暂停
        private bool isPalse = false;

        public Form1()
        {
            InitializeComponent();
            //初始化dtTemp列名
            dtTemp.Columns.Add("OrderID", typeof(string));
            dtTemp.Columns.Add("Side", typeof(OrderSide));
            dtTemp.Columns.Add("Price", typeof(string));
            dtTemp.Columns.Add("Quantity", typeof(string));
            dtTemp.Columns.Add("Status", typeof(OrderStatus));
            //初始化_dgvOrderbook列名
            _dgvOrderbook.Columns.Add("OrderID", "OrderID");
            _dgvOrderbook.Columns.Add("Side", "Side");
            _dgvOrderbook.Columns.Add("Price", "Price");
            _dgvOrderbook.Columns.Add("Quantity", "Quantity");
            _dgvOrderbook.Columns.Add("Status", "Status");
            _incomingOrderThread = new Thread(InComingWorkerProcessor);
            _incomingOrderThread.Start();
        }

        private Thread _incomingOrderThread;
        private void InComingWorkerProcessor()
        {
            var filestream = new System.IO.FileStream("OrderUpdates.txt",
                                          System.IO.FileMode.Open,
                                          System.IO.FileAccess.Read,
                                          System.IO.FileShare.ReadWrite);
            var file = new System.IO.StreamReader(filestream);
            String line;
            while ((line = file.ReadLine()) != null)
            {
                Random rand = new Random();
                int randNum = rand.Next() % 3000;
                Thread.Sleep(randNum);
                var splits = line.Split(',');
                OrderUpdate update = new OrderUpdate();
                update.OrderID = Convert.ToInt32(splits[0]);
                update.Side = (OrderSide)Enum.Parse(typeof(OrderSide), splits[1]);
                update.Price = Convert.ToDouble(splits[2]);
                update.Quantity = Convert.ToInt32(splits[3]);
                update.Status = (OrderStatus)Enum.Parse(typeof(OrderStatus), splits[4]);
                OnOrderUpdate(update);
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (button1.Text == "暂停")
            {
                //暂停 grid update 
                isPalse = true;
                button1.Text = "继续";

            }
            else if (button1.Text == "继续")
            {
                //继续 grid update
                isPalse = false;
                button1.Text = "暂停";
                //点击继续之后，将暂停期间保存的行全部显示到dgv
                //这段代码只能放在按钮相应方法，而不能放在OnOrderUpdate()，原因是后台读取完毕之后，将不再调用OnOrderUpdate()，从而无法处理dtTemp中剩余的行
                if (dtTemp.Rows.Count > 0)
                {
                    foreach (DataRow drx in dtTemp.Rows)
                    {
                        ProcessRow(drx);
                    }
                    dtTemp.Rows.Clear();
                }
            }
        }
        private void OnOrderUpdate(OrderUpdate update)
        {
            //TODO： 显示update到_dgvOrderbook 
            //用invoke和委托来解决线程安全的问题
            if (this._dgvOrderbook.InvokeRequired)
            {
                this.Invoke(new updateDelegate(OnOrderUpdate), new object[] { update });
            }
            else
            {
                DataRow dr = dtTemp.NewRow();
                dr[0] = update.OrderID;
                dr[1] = update.Side;
                dr[2] = update.Price;
                dr[3] = update.Quantity;
                dr[4] = update.Status;
                if (isPalse)
                {
                    //处于暂停状态时，当前行存入dtShow，直到按钮点击继续时，处理dtTemp中的行 
                    dtTemp.Rows.Add(dr);
                }
                else
                {   //处于进行状态时，直接处理当前行
                    ProcessRow(dr);

                }
            }
        }

        //以行为单位进行处理，显示到dgv
        private void ProcessRow(DataRow dr)
        {//判断OrderID是否重复，若重复，则设置对应的dgv中的index，若不重复则新增一行
            bool isRepeat = false;
            int indexOfDgvRow = 0;
            if (this._dgvOrderbook.Rows.Count > 1)
            {
                foreach (DataGridViewRow drx in this._dgvOrderbook.Rows)
                {
                    int indexTemp = this._dgvOrderbook.Rows.IndexOf(drx);
                    if (drx.Cells["OrderID"].FormattedValue.ToString() == (dr[0].ToString()))
                    {
                        indexOfDgvRow = indexTemp;
                        isRepeat = true;
                        break;
                    }
                }
            }
            if (!isRepeat)
                indexOfDgvRow = _dgvOrderbook.Rows.Add();
            //为dgv中的行赋值
            this._dgvOrderbook.Rows[indexOfDgvRow].Cells[0].Value = dr[0].ToString();
            this._dgvOrderbook.Rows[indexOfDgvRow].Cells[1].Value = Enum.GetName(typeof(OrderSide), dr[1]);
            this._dgvOrderbook.Rows[indexOfDgvRow].Cells[2].Value = dr[2].ToString();
            this._dgvOrderbook.Rows[indexOfDgvRow].Cells[3].Value = dr[3].ToString();
            this._dgvOrderbook.Rows[indexOfDgvRow].Cells[4].Value = Enum.GetName(typeof(OrderStatus), dr[4]);
            //设置行样式
            switch (int.Parse(dr[4].ToString()))
            {
                case (int)OrderStatus.Working:
                    _dgvOrderbook.Rows[indexOfDgvRow].DefaultCellStyle.BackColor = Color.White;
                    break;
                case (int)OrderStatus.Canceled:
                    _dgvOrderbook.Rows[indexOfDgvRow].DefaultCellStyle.BackColor = Color.Gray;
                    break;
                case (int)OrderStatus.Filled:
                    _dgvOrderbook.Rows[indexOfDgvRow].DefaultCellStyle.BackColor = Color.LightGray;
                    break;
                default:
                    break;
            }
            //设置单元格样式
            switch (int.Parse(dr[1].ToString()))
            {
                case (int)OrderSide.Sell:
                    _dgvOrderbook.Rows[indexOfDgvRow].Cells[1].Style.BackColor = Color.Green;
                    break;
                case (int)OrderSide.Buy:
                    _dgvOrderbook.Rows[indexOfDgvRow].Cells[1].Style.BackColor = Color.Red;
                    break;
                default:
                    break;
            }
        }
    }
}
