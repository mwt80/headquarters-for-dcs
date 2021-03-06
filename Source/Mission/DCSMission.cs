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

using System;
using System.Collections.Generic;
using System.Linq;

namespace Headquarters4DCS.Mission
{
    /// <summary>
    /// An HQ4DCS mission. Generated by MissionGenerator from a MissionTemplate.
    /// Must be exported to a .miz file by MIZExporter before being useable in DCS World.
    /// </summary>
    public sealed class DCSMission : IDisposable
    {
        /// <summary>
        /// The coalition each airbase belongs to. Key is the airbase ID in DCS World, Value is the coalition.
        /// </summary>
        public Dictionary<int, Coalition> AirbasesCoalition { get; set; } = new Dictionary<int, Coalition>();

        /// <summary>
        /// The mission name.
        /// </summary>
        public string BriefingName { get; set; }

        /// <summary>
        /// The mission description.
        /// </summary>
        public string BriefingDescription { get; set; }

        /// <summary>
        /// Should imperial units be used in this mission briefing instead of metric units?
        /// </summary>
        public bool BriefingImperialUnits { get; set; }

        /// <summary>
        /// The flight package to display in the briefing. A list of HQMissionBriefingFlightGroups.
        /// </summary>
        public List<DCSMissionBriefingFlightGroup> BriefingFlightPackage { get; set; } = new List<DCSMissionBriefingFlightGroup>();

        /// <summary>
        /// Briefing tasks, as a list of strings.
        /// </summary>
        public List<string> BriefingTasks { get; set; } = new List<string>();

        /// <summary>
        /// Briefing remarks, as a list of strings.
        /// </summary>
        public List<string> BriefingRemarks { get; set; } = new List<string>();

        /// <summary>
        /// The coalition the player belongs to.
        /// </summary>
        public Coalition CoalitionPlayer { get; set; }

        /// <summary>
        /// The enemy coalition. Read-only, generated from CoalitionPlayer.
        /// </summary>
        public Coalition CoalitionEnemy { get { return (Coalition)(1 - (int)CoalitionPlayer); } }

        /// <summary>
        /// The countries for each coalition.
        /// </summary>
        public DCSCountry[][] Countries { get; set; } = new DCSCountry[2][];

        /// <summary>
        /// The day of the month this mission takes place.
        /// </summary>
        public int DateDay { get; set; }

        /// <summary>
        /// The month during which this mission takes places.
        /// </summary>
        public Month DateMonth { get; set; }

        /// <summary>
        /// The year during which this mission takes place.
        /// </summary>
        public int DateYear { get; set; }

        /// <summary>
        /// Should red/blue countries be inverted on the map?
        /// </summary>
        public bool InvertTheaterCountries { get; set; }

        /// <summary>
        /// The language to use in this mission.
        /// </summary>
        public string Language { get; set; }

        /// <summary>
        /// Bullseye coordinates for each coalition.
        /// </summary>
        public Coordinates[] Bullseye { get; set; }

        /// <summary>
        /// Is this a single player mission?
        /// </summary>
        public bool SinglePlayer { get; set; }

        /// <summary>
        /// ID of the objective definition in the definition library.
        /// </summary>
        public string ObjectiveDefinition { get; set; }

        /// <summary>
        /// Should NATO callsigns be used in the briefing?
        /// </summary>
        public bool UseNATOCallsigns { get; set; }

        /// <summary>
        /// A list of all used player aircraft types. Used for briefing kneeboard generation, who must be stored in the KNEEBOARD/<Aircraft>/IMAGES directory, so one directory is required for each aircraft type.
        /// </summary>
        public string[] UsedPlayerAircraftTypes { get; set; }

        /// <summary>
        /// Generation log, from the mission generator which generated this mission.
        /// </summary>
        public string GenerationLog { get; set; } = "";

        /// <summary>
        /// The X,Y coordinates on which to center the F10 map.
        /// </summary>
        public Coordinates MapCenter { get; set; }

        /// <summary>
        /// The centerpoint of all mission objectives.
        /// </summary>
        public Coordinates ObjectivesCenterPoint
        { get { return Coordinates.GetCenter((from wp in Objectives select wp.Coordinates).ToArray()); } }

        /// <summary>
        /// The mission objectives.
        /// </summary>
        public DCSMissionObjectiveLocation[] Objectives { get; set; }

        /// <summary>
        /// An array of ogg files (read from HQ4DCS\Include\Ogg) to include in the .miz file.
        /// </summary>
        public string[] OggFiles { get; set; }

        /// <summary>
        /// Should external views be allowed?
        /// </summary>
        public DCSOption RealismAllowExternalViews { get; set; }

        /// <summary>
        /// Should bird strikes be enabled?
        /// </summary>
        public DCSOption RealismBirdStrikes { get; set; }

        /// <summary>
        /// Should random system failures be enabled?
        /// </summary>
        public DCSOption RealismRandomFailures { get; set; }

        /// <summary>
        /// The DCS World theater for this mission.
        /// </summary>
        public string TheaterDefinition { get; set; }

        /// <summary>
        /// The time period during which this mission takes place.
        /// Read-only, generated from DateYear.
        /// </summary>
        public TimePeriod TimePeriod
        {
            get
            {
                TimePeriod period = TimePeriod.Decade1940;

                foreach (TimePeriod tp in (TimePeriod[])Enum.GetValues(typeof(TimePeriod)))
                    if (DateYear >= (int)tp) period = tp;

                return period;
            }
        }

        /// <summary>
        /// Starting time of the mission, hours.
        /// </summary>
        public int TimeHour { get; set; }

        /// <summary>
        /// Starting time of the mission, minutes.
        /// </summary>
        public int TimeMinute { get; set; }

        /// <summary>
        /// Starting time of the mission as seconds since midnight.
        /// Read-only. The sum of TimeHour (x3600) and TimeMinute (x60).
        /// </summary>
        public int TimeTotalSeconds { get { return HQTools.Clamp(TimeHour * 3600 + TimeMinute * 60, 0, HQTools.SECONDS_PER_DAY - 1); } }

        /// <summary>
        /// Mission unit groups.
        /// </summary>
        public List<DCSMissionUnitGroup> UnitGroups { get; set; } = new List<DCSMissionUnitGroup>();

        /// <summary>
        /// Total length of the flight plan, in meters.
        /// </summary>
        public double TotalFlightPlanDistance { get; set; }

        /// <summary>
        /// Mission waypoints.
        /// </summary>
        public DCSMissionWaypoint[] Waypoints { get; set; }

        /// <summary>
        /// Overall weather setting. Not used by DCS World, only used internally and for briefings.
        /// </summary>
        public Weather WeatherLevel { get; set; }

        /// <summary>
        /// Overall wind speed. Not used by DCS World, only used internally and for briefings.
        /// </summary>
        public Wind WindLevel { get; set; }

        /// <summary>
        /// The type of precipitation.
        /// </summary>
        public Precipitation WeatherCloudsPrecipitation { get; set; }

        /// <summary>
        /// Is dust enabled?
        /// </summary>
        public bool WeatherDustEnabled { get; set; }

        /// <summary>
        /// Is fog enabled?
        /// </summary>
        public bool WeatherFogEnabled { get; set; }

        /// <summary>
        /// The base altitude for clouds, in meters.
        /// </summary>
        public int WeatherCloudBase { get; set; }

        /// <summary>
        /// The density of the cloud layer (0-10).
        /// </summary>
        public int WeatherCloudsDensity { get; set; }

        /// <summary>
        /// The thickness of the clouds (in meters).
        /// </summary>
        public int WeatherCloudsThickness { get; set; }

        /// <summary>
        /// The density of the dust (0-10).
        /// </summary>
        public int WeatherDustDensity { get; set; }

        /// <summary>
        /// The thickness of the fog.
        /// </summary>
        public int WeatherFogThickness { get; set; }

        /// <summary>
        /// Visibiliy in the fog (in meters).
        /// </summary>
        public int WeatherFogVisibility { get; set; }

        /// <summary>
        /// QNH (atmospheric pressure adjusted to mean sea level)
        /// </summary>
        public int WeatherQNH { get; set; }

        /// <summary>
        /// Temperature (in Celsius degrees).
        /// </summary>
        public int WeatherTemperature { get; set; }

        /// <summary>
        /// Turbulence (in m/s)
        /// </summary>
        public int WeatherTurbulence { get; set; }

        /// <summary>
        /// Visibility (in meters)
        /// </summary>
        public int WeatherVisibility { get; set; }

        /// <summary>
        /// Wind direction, in degrees (at 0, 2000 and 8000 meters)
        /// </summary>
        public int[] WeatherWindDirection { get; set; } = new int[3];

        /// <summary>
        /// Wind speed, in m/s (at 0, 2000 and 8000 meters)
        /// </summary>
        public int[] WeatherWindSpeed { get; set; } = new int[3];

        /// <summary>
        /// IDispose implementation.
        /// </summary>
        public void Dispose() { }

        /// <summary>
        /// The briefing, in HTML format.
        /// </summary>
        public string BriefingHTML { get; set; } = "";

        /// <summary>
        /// The briefing, in raw text.
        /// </summary>
        public string BriefingRawText { get; set; } = "";

        /// <summary>
        /// The average wind speed at 0, 2000 and 8000 meters altitude.
        /// </summary>
        public int WeatherWindSpeedAverage
        { get { return (WeatherWindSpeed[0] + WeatherWindSpeed[1] + WeatherWindSpeed[2]) / 3; } }
    }
}
