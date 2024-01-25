using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CitizenFX.Core;
using FivePD.API;
using FivePD.API.Utils;
using Newtonsoft.Json.Linq;

namespace FivePD_Anti_Despawn_Entities;

public class Main : Plugin
{
    private bool dutyStatus = false;
    bool trafficStop = false;
    
    internal Main()
    {
        Events.OnDutyStatusChange += EventsOnOnDutyStatusChange;
        EventHandlers["FIVEPD::Client::changePedState"] += ChangePedState;
    }

    private List<Ped> stoppedEntities = new List<Ped>(); // Persistent

    private async Task ChangePedState(string rawdata)
    {
        try
        {
            if (trafficStop) return;
            JObject data = JObject.Parse(rawdata);
            int netId = (int)data["networkId"];
            Ped ped = (Ped)Entity.FromNetworkId(netId);
            if (ped == null)
            {
                Debug.WriteLine("~r~ERROR: PED IS NULL");
                return;
            }

            Ped closestPlayer = getClosestPlayerToPed(ped);
            if (Game.PlayerPed.NetworkId != closestPlayer.NetworkId) return;
            bool isCuffed = false;
            if (data["isArrested"] != null)
            {
                isCuffed = (bool)data["isArrested"];
            }

            bool isStopped = false;
            if (data["isStopped"] != null)
            {
                isStopped = (bool)data["isStopped"];
            }

            bool remove = false;
            if (data["remove"] != null)
            {
                remove = (bool)data["remove"];
            }

            if (isStopped || isCuffed && !remove)
            {
                if (!ped.IsPersistent)
                    ped.IsPersistent = true;
                if (!stoppedEntities.Contains(ped))
                    stoppedEntities.Add(ped);
            }

            if (remove)
            {
                if (ped.IsPersistent)
                    ped.IsPersistent = false;
                if (stoppedEntities.Contains(ped))
                    stoppedEntities.Remove(ped);
            }
        }
        catch (Exception ex)
        {
            return;
        }
       
    }

    private Ped getClosestPlayerToPed(Ped ped)
    {
        Ped result = null;
        foreach (Player player in Players)
        {
            if (player == null) continue;
            if (result == null)
                result = player.Character;
            if (player.Character.Position.DistanceTo(ped.Position) < result.Position.DistanceTo(ped.Position))
                result = player.Character;
        }

        return result;
    }

    private async Task EventsOnOnDutyStatusChange(bool onduty)
    {
        dutyStatus = onduty;
       
        List<Entity> persistentEntities = new List<Entity>();
        Tick += async () =>
        {
            if (!dutyStatus) return;
            try
            {
                if (!trafficStop && Utilities.IsPlayerPerformingTrafficStop())
                {
                    trafficStop = true;
                    Ped driver = Utilities.GetDriverFromTrafficStop();
                    Vehicle veh = Utilities.GetVehicleFromTrafficStop();
                    Ped[] passengers = Utilities.GetPassengersFromTrafficStop();

                    if (driver != null && driver.Exists())
                    {
                        driver.IsPersistent = true;
                        if (!persistentEntities.Contains(driver))
                            persistentEntities.Add(driver);
                    }

                    if (veh != null && veh.Exists())
                    {
                        veh.IsPersistent = true;
                        if (!persistentEntities.Contains(veh))
                            persistentEntities.Add(veh);
                    }

                    if (passengers != null && passengers.Length > 0)
                    {
                        foreach (var passenger in passengers)
                        {
                            if (passenger != null && passenger.Exists())
                            {
                                passenger.IsPersistent = true;
                                if (!persistentEntities.Contains(passenger))
                                    persistentEntities.Add(passenger);
                            }
                        }
                    }
                }
                else if (trafficStop && !Utilities.IsPlayerPerformingTrafficStop())
                {
                    trafficStop = false;
                    foreach (var persistentEntity in persistentEntities.ToArray())
                    {
                        if (persistentEntity == null || !persistentEntity.Exists()) continue;
                        persistentEntity.IsPersistent = false;
                        if (persistentEntities.Contains(persistentEntity))
                            persistentEntities.Remove(persistentEntity);
                    }
                }
            }
            catch (Exception err)
            {
                return;
            }
           
        };
    }
}