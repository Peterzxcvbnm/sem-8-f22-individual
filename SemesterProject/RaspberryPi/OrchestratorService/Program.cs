using System;
using Entities;
using Mqtt;

#region Variables
IMqttService mqtt = new MqttService();

Dictionary<string, Crane> cranes = new();
Dictionary<string, Disk> disks = new();
Dictionary<string, Camera> cameras = new();

bool running = true;
#endregion

#region Main
Setup();
Run().GetAwaiter().GetResult();
#endregion

void Setup()
{
	cranes.Add("crane1", new Crane("crane1", new Dictionary<string, int>()
	{
		{"pickupPos", 0},
		{"outRed", 30},
		{"outGreen", 60},
		{"outBlue", 90}
	}, mqtt));

	disks.Add("disk1", new Disk("disk1", 8, new Dictionary<string, int>()
	{
		{"craneZone", 1},
		{"cameraZone", 3},
		{"intakeZone", 6}
	}, mqtt));

	cameras.Add("camera1", new Camera("camera1", new List<string>()
	{
		"blue",
		"green",
		"red"
	}, mqtt));
}

async Task Run()	
{
	var crane1 = cranes["crane1"];
	var disk1 = disks["disk1"];
	var camera1 = cameras["camera1"];

	while (running)
	{
		foreach (var diskSlot in disk1.GetSlotsWithMark(SlotState.Complete))
		{
			mqtt.SendMessage(MqttTopics.Orchestrator.LogVariable + "information", $"Complete slot: {diskSlot} of disk1");
			await disk1.MoveSlot(diskSlot.Number, "craneZone");
			await crane1.GoTo("pickupPos");
			await crane1.PickupItem();
			if (diskSlot.HasMark("RED"))
			{
				await crane1.GoTo("outRed");
				await crane1.DropItem();
			}
			if (diskSlot.HasMark("GREEN"))
			{
				await crane1.GoTo("outGreen");
				await crane1.DropItem();
			}
			if (diskSlot.HasMark("BLUE"))
			{
				await crane1.GoTo("outBlue");
				await crane1.DropItem();
			}
			disk1.MarkSlot("craneZone", SlotState.Empty);
		}
		if (!disk1.IsFull())
		{
			mqtt.SendMessage(MqttTopics.Orchestrator.LogDeviceState + "information", disk1.GetState());
			await disk1.MoveSlot(disk1.GetEmptySlotNumber(), "intakeZone");
			await disk1.WaitForIntake();
			disk1.MarkSlot("intakeZone", SlotState.InProgress);
			await disk1.MoveSlot("intakeZone", "cameraZone");
			var currentItemColor = await camera1.Scan();
			mqtt.SendMessage(MqttTopics.Orchestrator.LogVariable + "information", $"Scanned colour: {currentItemColor}");
			disk1.MarkSlot("cameraZone", currentItemColor);
			if (currentItemColor == "RED")
			{
				Task.Run(async () =>
				{
					await Task.Delay(10000);
					disk1.MarkSlot("cameraZone", SlotState.Complete);
				});
			}
			if (currentItemColor == "GREEN")
			{
				Task.Run(async () =>
				{
					await Task.Delay(20000);
					disk1.MarkSlot("cameraZone", SlotState.Complete);
				});
			}
			if (currentItemColor == "BLUE")
			{
				Task.Run(async () =>
				{
					await Task.Delay(30000);
					disk1.MarkSlot("cameraZone", SlotState.Complete);
				});
			}
		}
	}
}
