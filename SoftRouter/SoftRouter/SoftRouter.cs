﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpPcap;
using PacketDotNet;
using SharpPcap.WinPcap;
using System.Net;
using System.Threading;
using System.Net.NetworkInformation;
using System.Windows.Forms;

namespace SoftRouter
{
	public class SoftRouter
	{
		#region 存储IP -> MAC 映射关系
		private Dictionary<IPAddress, PhysicalAddress> macAddress;
		#endregion

		#region 存储可用设备列表
		public List<Device> deviceList;
		#endregion

		#region 存储已处理IP包列表
		private List<ushort> hadHandledIpList;
		#endregion

		#region 静态路由信息
		private RouteTableList staticRouting;
		#endregion

		public RichTextBox outputWindow;

		public SoftRouter()
		{
			macAddress = new Dictionary<IPAddress, PhysicalAddress>();
			deviceList = Device.GetDeviceList();
			hadHandledIpList = new List<ushort>();
			staticRouting = new RouteTableList();
		}

		public void StartCapture()
		{
			foreach (Device dev in deviceList)
			{
				dev.Interface.OnPacketArrival += OnPacketArrval;
				dev.Interface.StartCapture();
			}
		}

		public void StopCapture()
		{
			foreach (Device dev in deviceList)
			{
				dev.Interface.StopCapture();
			}
		}

		public void CloseDevice()
		{
			foreach (Device dev in deviceList)
			{
				dev.Interface.Close();
			}
		}

		#region 数据包捕获处理
		private void OnPacketArrval(object sender, CaptureEventArgs e)
		{
			if (e.Packet.LinkLayerType == LinkLayers.Ethernet)
			{
				try
				{
					EthernetPacket eth = (EthernetPacket)EthernetPacket.ParsePacket(e.Packet.LinkLayerType, e.Packet.Data);

					if (eth.PayloadPacket is ARPPacket)
					{
						ARPPacket arp = (ARPPacket)eth.PayloadPacket;

						if (arp.Operation == ARPOperation.Response || arp.Operation == ARPOperation.Request)
						{
							macAddress.Add(arp.SenderProtocolAddress, arp.SenderHardwareAddress);
						}
					}
					else if (eth.PayloadPacket is IPv4Packet)
					{
						IPv4Packet ip = (IPv4Packet)eth.PayloadPacket;

						if (hadHandledIpList.Contains(ip.Id))
						{
							return;
						}
						hadHandledIpList.Add(ip.Id);

						Thread thread = new Thread(() =>
						{
							
							if (outputWindow != null)
							{
								string info = string.Format("time:{0}:{1}:{2}/{5}  {3} -> {4}  type:{6}\n", DateTime.Now.Hour,
									DateTime.Now.Minute, DateTime.Now.Second, ip.SourceAddress, ip.DestinationAddress, DateTime.Now.Millisecond, ip.Protocol);
								outputWindow.AppendText(info);
								outputWindow.Focus();
								outputWindow.Select(outputWindow.TextLength, 0);
							}
						});
						thread.IsBackground = true;
						thread.Start();

						bool hadSent = false;

						#region 直连路由包
						foreach (Device dev in deviceList)
						{
							if (GetNetIpAddress(ip.DestinationAddress, dev.MaskAddress).ToString() == dev.NetAddress.ToString())
							{
								eth.SourceHwAddress = dev.MacAddress;
								if (!macAddress.ContainsKey(ip.DestinationAddress))
								{
									MacAddress.GetMacAddress(ip.DestinationAddress);
									return;
								}
								else
								{
									eth.DestinationHwAddress = macAddress[ip.DestinationAddress];
								}
								dev.Interface.SendPacket(eth);
								hadSent = true;
							}
						}
						if (hadSent)
						{
							return;
						}
						#endregion

						#region 非直连路由包
						RouteTable route = staticRouting[ip.DestinationAddress];
						if (route != null)
						{
							eth.SourceHwAddress = route.OutInterface.MacAddress;
							if (!macAddress.ContainsKey(route.NextHop))
							{
								MacAddress.GetMacAddress(route.NextHop);
								return;
							}
							else
							{
								eth.DestinationHwAddress = macAddress[route.NextHop];
							}
							route.OutInterface.SendPacket(eth);
						}
						#endregion

					}
					else if (eth.PayloadPacket is PPPoEPacket)
					{
						//暂不处理
					}
				}
				catch
				{
					//Protocol of 49185 is not implemented
					//不支持此协议,对此类协议包进行忽略
					return;
				}
			}
		}
		#endregion

		#region 根据IP地址与子网掩码获取网络地址
		static public IPAddress GetNetIpAddress(IPAddress ip, IPAddress mask)
		{
			var byte1 = ip.GetAddressBytes();
			var byte2 = mask.GetAddressBytes();
			return new IPAddress(new byte[] { (byte)(byte1[0] & byte2[0]), (byte)(byte1[1] & byte2[1]), 
				(byte)(byte1[2] & byte2[2]), (byte)(byte1[3] & byte2[3]) });
		}
		#endregion

		public RouteTableList StaticRouting
		{
			get
			{
				return staticRouting;
			}
		}
	}
}
