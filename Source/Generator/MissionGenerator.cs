﻿/*
==========================================================================
This file is part of Headquarters for DCS World (HQ4DCS), a mission generator for
Eagle Dynamics' DCS World flight simulator.

HQ4DCS was created by Ambroise Garel (@akaAgar).
You can find more information about the project on its GitHub page,
https://akaAgar.github.io/headquarters-for-dcs

HQ4DCS is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

HQ4DCS is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with HQ4DCS. If not, see https://www.gnu.org/licenses/
==========================================================================
*/

using Headquarters4DCS.DefinitionLibrary;
using Headquarters4DCS.Mission;
using Headquarters4DCS.Template;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Headquarters4DCS.Generator
{
    /// <summary>
    /// Mission generator. Turns a MissionTemplate into an HQMission.
    /// </summary>
    public sealed class MissionGenerator : IDisposable
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public MissionGenerator()
        {
            //CSGenerator = new CallsignGenerator();
        }

        /// <summary>
        /// IDispose implementation.
        /// </summary>
        public void Dispose() { }

        public DCSMission Generate(MissionTemplate template, out string errorMessage)
        {
            int i;
            errorMessage = "";

            // Clear log, begin timing then create an instance of the HQ mission class
            Stopwatch stopwatch = new Stopwatch(); stopwatch.Start();
            DCSMission mission = new DCSMission();
            DebugLog.Instance.Clear();
            DebugLog.Instance.Log($"STARTING MISSION GENERATION AT {DateTime.Now.ToLongTimeString()}...");
            DebugLog.Instance.Log(new string('=', DebugLog.Instance.GetLastMessage().Length));
            DebugLog.Instance.Log();

            try
            {
                using (MissionGeneratorTemplateChecker templateChecker = new MissionGeneratorTemplateChecker())
                { templateChecker.CheckTemplate(template); }

                if (template.GetPlayerCount() < 1)
                    throw new HQ4DCSException("Mission must include at least one player-controlled aircraft.");

                // Pick definitions
                DefinitionCoalition[] coalitions = new DefinitionCoalition[2];
                coalitions[(int)Coalition.Blue] = Library.Instance.GetDefinition<DefinitionCoalition>(template.ContextCoalitionBlue);
                coalitions[(int)Coalition.Red] = Library.Instance.GetDefinition<DefinitionCoalition>(template.ContextCoalitionRed);

                DefinitionLanguage languageDef = Library.Instance.GetDefinition<DefinitionLanguage>(template.PreferencesLanguage.ToLowerInvariant());
                DefinitionObjective objectiveDef = Library.Instance.GetDefinition<DefinitionObjective>(template.ObjectiveType.ToLowerInvariant());
                DefinitionTheater theaterDef = Library.Instance.GetDefinition<DefinitionTheater>(template.ContextTheater);
                theaterDef.ResetUsedSpawnPoints();

                // Create a list of all available objective names
                List<string> objectiveNames = languageDef.GetStringArray("Mission", "Waypoint.ObjectiveNames").ToList();

                // Create unit generators
                MissionGeneratorCallsign callsignGenerator = new MissionGeneratorCallsign(coalitions[(int)Coalition.Blue].NATOCallsigns, coalitions[(int)Coalition.Red].NATOCallsigns);
                MissionGeneratorUnitGroups unitGroupsGenerator = new MissionGeneratorUnitGroups(languageDef, callsignGenerator);

                // Copy values from the template to the mission
                mission.TheaterDefinition = template.ContextTheater;
                mission.ObjectiveDefinition = template.ObjectiveType;
                mission.Language = template.PreferencesLanguage;
                mission.CoalitionPlayer = template.ContextPlayerCoalition;
                mission.SinglePlayer = (template.GetPlayerCount() < 2);
                mission.UseNATOCallsigns = coalitions[(int)template.ContextPlayerCoalition].NATOCallsigns;

                // Make sure no countries are shared between both coalitions
                mission.Countries[(int)Coalition.Blue] = coalitions[(int)Coalition.Blue].Countries.ToArray();
                mission.Countries[(int)Coalition.Red] = coalitions[(int)Coalition.Red].Countries.Except(coalitions[(int)Coalition.Blue].Countries).ToArray();

                if (mission.Countries[(int)Coalition.Red].Length == 0)
                    throw new HQ4DCSException("Red and blue coalitions cannot share the same countries.");

                switch (template.BriefingUnits)
                {
                    case UnitSystem.ByCoalition:
                        mission.BriefingImperialUnits = (coalitions[(int)mission.CoalitionPlayer].UnitSystem == UnitSystem.Imperial); break;
                    case UnitSystem.Imperial: mission.BriefingImperialUnits = true; break;
                    case UnitSystem.Metric: mission.BriefingImperialUnits = false; break;
                }

                // Generate mission environment parameters (weather, time of day, date...)
                using (MissionGeneratorEnvironment environment = new MissionGeneratorEnvironment())
                {
                    environment.GenerateMissionDate(mission, template.ContextTimePeriod, template.EnvironmentSeason);
                    environment.GenerateMissionTime(mission, template.EnvironmentTimeOfDay, theaterDef);
                    environment.GenerateWeather(mission, template.EnvironmentWeather, theaterDef);
                    environment.GenerateWind(mission, template.EnvironmentWind, theaterDef);
                }

                // Randomly select players' airbase
                DefinitionTheaterAirbase missionAirbase = HQTools.RandomFrom((from DefinitionTheaterAirbase ab in theaterDef.Airbases where ab.Coalition == template.ContextPlayerCoalition select ab).ToArray());

                // Randomly select objective spawn points
                int objectiveCount = (int)template.ObjectiveCount;
                if (objectiveCount == 0) objectiveCount = HQTools.RandomFrom(1, 1, 1, 2, 2, 3, 3, 4, 5); // Random objective count
                //AmountR objectiveDistance = template.ObjectiveDistance;
                //if (objectiveDistance == AmountR.Random) objectiveDistance =
                //        HQTools.RandomFrom(AmountR.VeryLow, AmountR.VeryLow, AmountR.Low, AmountR.Low, AmountR.Low, AmountR.Average, AmountR.Average, AmountR.Average, AmountR.High, AmountR.High, AmountR.VeryHigh);
                List<DCSMissionObjectiveLocation> objectivesList = new List<DCSMissionObjectiveLocation>();
                List<DCSMissionWaypoint> waypointsList = new List<DCSMissionWaypoint>();
                for (i = 0; i < objectiveCount; i++)
                {
                    // If this is the first objective, measure distance from the airbase. Else measure distance from the previous objective.
                    Coordinates previousPoint = (i == 0) ? missionAirbase.Coordinates : objectivesList[i - 1].Coordinates;

                    MinMaxD distanceFromLastPoint = new MinMaxD(template.ObjectiveDistance * 0.75, template.ObjectiveDistance * 1.25) * HQTools.NM_TO_METERS;
                    if (i > 0) distanceFromLastPoint /= 4.0;

                    DefinitionTheaterSpawnPoint? spawnPoint =
                        theaterDef.GetRandomSpawnPoint(objectiveDef.SpawnPointType, null, distanceFromLastPoint, previousPoint);

                    if (!spawnPoint.HasValue) // No valid spawn point, throw an error
                        throw new HQ4DCSException($"Cannot find a valid spawn point for objective #{i + 1}");

                    // Select a random name for the objective
                    string objName;
                    if (objectiveNames.Count == 0) objName = $"OBJECTIVE{(i + 1).ToString("00")}";
                    else
                    {
                        objName = HQTools.RandomFrom(objectiveNames);
                        objectiveNames.Remove(objName);
                    }

                    objectivesList.Add(new DCSMissionObjectiveLocation(spawnPoint.Value.Coordinates, objName, objectiveDef.WaypointOnGround ? 0.0 : 1.0, 0));

                    // Add a waypoint for each objective
                    waypointsList.Add(new DCSMissionWaypoint(spawnPoint.Value.Coordinates + Coordinates.CreateRandomInaccuracy(objectiveDef.WaypointInaccuracy), objName));
                }

                // If required, add additional waypoints on the way to & from the objectives
                if (template.PreferencesExtraWaypoints && (waypointsList.Count > 0))
                {
                    Coordinates firstWPCoos = waypointsList.First().Coordinates;
                    Coordinates lastWPCoos = waypointsList.Last().Coordinates;

                    int wpBeforeCount = HQTools.RandomMinMax(1, 3);
                    for (i = 0; i < wpBeforeCount; i++)
                        waypointsList.Insert(i,
                            new DCSMissionWaypoint(
                                Coordinates.Lerp(missionAirbase.Coordinates, firstWPCoos, (double)(i + 1) / (wpBeforeCount + 1)) +
                                Coordinates.CreateRandomInaccuracy(firstWPCoos.GetDistanceFrom(missionAirbase.Coordinates) * 0.05, firstWPCoos.GetDistanceFrom(missionAirbase.Coordinates) * 0.15),
                                $"WP{(i + 1).ToString()}"));

                    int wpAfterCount = HQTools.RandomMinMax(1, 2);
                    for (i = 0; i < wpAfterCount; i++)
                        waypointsList.Add(
                            new DCSMissionWaypoint(
                                Coordinates.Lerp(lastWPCoos, missionAirbase.Coordinates, (double)(i + 1) / (wpAfterCount + 1)) +
                                Coordinates.CreateRandomInaccuracy(lastWPCoos.GetDistanceFrom(missionAirbase.Coordinates) * 0.05, lastWPCoos.GetDistanceFrom(missionAirbase.Coordinates) * 0.15),
                                $"WP{(waypointsList.Count + 1).ToString()}"));
                }

                mission.Objectives = objectivesList.ToArray();
                mission.Waypoints = waypointsList.ToArray();

                mission.TotalFlightPlanDistance = 0.0;
                for (i = 0; i <= mission.Waypoints.Length; i++)
                {
                    if (i == 0) // first point, add distance between the takeoff airbase and the first waypoint
                        mission.TotalFlightPlanDistance += missionAirbase.Coordinates.GetDistanceFrom(mission.Waypoints.First().Coordinates);
                    else if (i == mission.Waypoints.Length) // last point, add distance between last waypoint and landing airbase
                        mission.TotalFlightPlanDistance += missionAirbase.Coordinates.GetDistanceFrom(mission.Waypoints.Last().Coordinates);
                    else // any other point, add distance between this waypoint and the last one
                        mission.TotalFlightPlanDistance += mission.Waypoints[i].Coordinates.GetDistanceFrom(mission.Waypoints[i - 1].Coordinates);
                }

                // Create a list of used player aircraft types, so the proper kneeboard subdirectories can be created in the .miz file
                mission.UsedPlayerAircraftTypes =
                    (from MissionTemplatePlayerFlightGroup pfg in template.FlightPackagePlayers select pfg.AircraftType).Distinct().OrderBy(x => x).ToArray();

                // Generate bullseyes and map center
                mission.MapCenter = Coordinates.GetCenter(
                    (from DCSMissionObjectiveLocation o in mission.Objectives select o.Coordinates).Union(new Coordinates[] { missionAirbase.Coordinates }).ToArray());
                mission.Bullseye = new Coordinates[2];
                for (i = 0; i < 2; i++)
                    mission.Bullseye[i] = mission.MapCenter + Coordinates.CreateRandomInaccuracy(10000, 20000);

                // Copy scripts
                //mission.ScriptsMission = missionObjective.ScriptMission.ToList();
                //mission.ScriptsObjective = missionObjective.ScriptObjective.ToList();

                mission.RealismAllowExternalViews = template.RealismAllowExternalViews;
                mission.RealismBirdStrikes = template.RealismBirdStrikes;
                mission.RealismRandomFailures = template.RealismRandomFailures;

                // Create list of airbase alignment from the theater definition
                mission.AirbasesCoalition.Clear();
                foreach (DefinitionTheaterAirbase ab in theaterDef.Airbases)
                {
                    if (mission.AirbasesCoalition.ContainsKey(ab.DCSID)) continue;

                    Coalition airbaseCoalition = ab.Coalition;
                    switch (template.ContextCountriesCoalitions)
                    {
                        case CountriesCoalition.AllBlue: airbaseCoalition = Coalition.Blue; break;
                        case CountriesCoalition.AllRed: airbaseCoalition = Coalition.Red; break;
                        case CountriesCoalition.Inverted: airbaseCoalition = (Coalition)(1 - (int)ab.Coalition); break;
                    }

                    mission.AirbasesCoalition.Add(ab.DCSID, airbaseCoalition);
                }

                // Make sure the starting airbase belongs to the players' coalition no matter which coalition other airbases belong to
                if (mission.AirbasesCoalition.ContainsKey(missionAirbase.DCSID))
                    mission.AirbasesCoalition[missionAirbase.DCSID] = template.ContextPlayerCoalition;

                List<string> oggFilesList = new List<string>();
                oggFilesList.AddRange(Library.Instance.Common.SharedOggFiles); // Default wave files
                oggFilesList.AddRange(objectiveDef.IncludeOgg); // Objective wave files
                mission.OggFiles = oggFilesList.Distinct().ToArray();

                //mission.Scripts

                /*
                // Generate mission flight plan
                using (GeneratorFlightPlan flightPlan = new GeneratorFlightPlan(Library, language, csGenerator))
                {
                    flightPlan.SelectTakeoffAndLandingAirbases(mission, theater);
                    mission.MapCenter = mission.Airbases[0].Coordinates; // Center the map on the starting airdrome
                    flightPlan.GenerateObjectiveLocations(mission, template, theater, missionObjective);
                    flightPlan.GenerateWaypoints(mission, template, theater, missionObjective);
                    flightPlan.GenerateBullseye(mission);
                }
                */

                // Generate units
                AmountNR selectedEnemyAirDefense, selectedEnemyCAP; // We have to store these values here because they're used in the briefing remarks
                using (MissionGeneratorUnitGroups unitGenerator = new MissionGeneratorUnitGroups(languageDef, callsignGenerator))
                {
                    foreach (MissionTemplatePlayerFlightGroup pfg in template.FlightPackagePlayers)
                        unitGenerator.AddPlayerFlightGroup(mission, template, pfg, objectiveDef, missionAirbase);

                    //unitGroups.GeneratePlayerFlightGroups(mission, template, missionObjective);
                    //unitGroups.GenerateAIEscortFlightGroups(mission, template, coalitions, template.FlightGroupsAICAP, UnitFamily.PlaneFighter, "GroupPlaneEscortCAP", AircraftPayloadType.A2A, DCSAircraftTask.CAP);
                    //unitGroups.GenerateAIEscortFlightGroups(mission, template, coalitions, template.FlightGroupsAISEAD, UnitFamily.PlaneSEAD, "GroupPlaneEscortSEAD", AircraftPayloadType.SEAD, DCSAircraftTask.SEAD);

                    unitGenerator.AddObjectiveUnitGroupsAtEachObjective(mission, template, objectiveDef, coalitions);
                    //unitGroups.GenerateObjectiveUnitGroupsAtCenter(mission, template, missionObjective, coalitions);
                    unitGenerator.AddFriendlySupportAircraft(mission, template, coalitions[(int)mission.CoalitionPlayer], theaterDef, missionAirbase);
                    unitGenerator.AddEnemyAirDefenseUnits(mission, template, theaterDef, objectiveDef, coalitions, missionAirbase, out selectedEnemyAirDefense);
                    unitGenerator.AddFriendlyAirDefenseUnits(mission, template, theaterDef, objectiveDef, coalitions, missionAirbase, out AmountNR selectedFriendlyAirDefense);
                    unitGenerator.AddCombatAirPatrolUnits(mission, template, theaterDef, coalitions, missionAirbase, out AmountNR selectedFriendlyCAP, out selectedEnemyCAP);
                }

                using (MissionGeneratorBriefing briefingGenerator = new MissionGeneratorBriefing(languageDef))
                {
                    // Add briefing remarks
                    for (i = 0; i < objectiveDef.BriefingRemarks.Length; i++)
                        mission.BriefingRemarks.Add(languageDef.GetStringRandom("Briefing", $"Remark.{objectiveDef.BriefingRemarks}"));
                    mission.BriefingRemarks.Add(languageDef.GetStringRandom("Briefing", $"Remark.EnemyAirDefense.{selectedEnemyAirDefense}"));
                    mission.BriefingRemarks.Add(languageDef.GetStringRandom("Briefing", $"Remark.EnemyCAP.{selectedEnemyCAP}"));

                    mission.BriefingTasks.Add(languageDef.GetString("Briefing", "Task.TakeOffFrom", "Airbase", missionAirbase.Name));
                    for (i = 0; i < mission.Objectives.Length; i++)
                        mission.BriefingTasks.Add(languageDef.GetString("Briefing", $"Task.{objectiveDef.BriefingTask}", "Objective", mission.Objectives[i].Name.ToUpperInvariant()));
                    mission.BriefingTasks.Add(languageDef.GetString("Briefing", "Task.LandAt", "Airbase", missionAirbase.Name));

                    briefingGenerator.GenerateMissionName(mission, template.BriefingName);
                    briefingGenerator.GenerateMissionDescription(mission, template.BriefingDescription, objectiveDef);
                    /*
                        briefing.GenerateMissionTasks(mission, template, missionObjective);
                        briefing.GenerateMissionRemarks(mission, template, missionObjective);
                        */
                    briefingGenerator.GenerateRawTextBriefing(mission, template/*, missionObjective*/);
                    briefingGenerator.GenerateHTMLBriefing(mission, template/*, missionObjective*/);
                }

                stopwatch.Stop();
                DebugLog.Instance.Log();
                DebugLog.Instance.Log($"COMPLETED MISSION GENERATION AT {DateTime.Now.ToLongTimeString()} (TOOK {stopwatch.Elapsed.TotalMilliseconds.ToString("F0")} MILLISECONDS).");
                DebugLog.Instance.Log();
                mission.GenerationLog = DebugLog.Instance.GetFullLog();
            }
#if DEBUG
            catch (HQ4DCSException e)
#else
            catch (Exception e)
#endif
            {
                stopwatch.Stop();
                DebugLog.Instance.Log($"ERROR: {e.Message}");
                DebugLog.Instance.Log();
                DebugLog.Instance.Log($"MISSION GENERATION FAILED.");
                DebugLog.Instance.Log();
                errorMessage = e.Message;

                mission.Dispose();
                mission = null;
            }

            DebugLog.Instance.SaveToFileAndClear("MissionGeneration");
            return mission;
        }
    }
}
