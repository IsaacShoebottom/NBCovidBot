﻿using Discord;
using Microsoft.Extensions.Configuration;
using NBCovidBot.Covid.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TimeZoneConverter;

namespace NBCovidBot.Covid
{
    public class CovidDataFormatter
    {
        private readonly CovidDataProvider _dataProvider;
        private readonly IConfiguration _configuration;

        public CovidDataFormatter(CovidDataProvider dataProvider,
            IConfiguration configuration)
        {
            _dataProvider = dataProvider;
            _configuration = configuration;
        }

        private DateTimeOffset GetDate(long timestamp)
        {
            var timeZone = TZConvert.GetTimeZoneInfo(_configuration["TimeZone"]);
            var date = DateTimeOffset.FromUnixTimeMilliseconds(timestamp);
            return TimeZoneInfo.ConvertTime(date, timeZone);
        }

        private static string JoinRows(int spacing, params string[][] rows)
        {
            var numCols = rows.Max(x => x.Length);

            var maxColLens = new int[numCols];

            foreach (var row in rows)
            {
                for (var j = 0; j < numCols; j++)
                {
                    if (row.Length <= j)
                        break;

                    if (row[j].Length > maxColLens[j])
                        maxColLens[j] = row[j].Length;
                }
            }

            var builder = new StringBuilder();

            foreach (var row in rows)
            {
                for (var j = 0; j < numCols; j++)
                {
                    var spaces = maxColLens[j] + spacing;

                    if (row.Length > j)
                    {
                        builder.Append(row[j]);
                        spaces -= row[j].Length;
                    }

                    builder.Append(new string(' ', spaces));
                }

                builder.AppendLine();
            }

            return builder.ToString();
        }

        public Embed GetEmbed()
        {
            var zonesDailyInfo = _dataProvider.GetZonesDailyInfo()
                ?.OrderByDescending(x => x.ActiveCases).ToList();

            var provinceDailyInfo = _dataProvider.GetProvinceDailyInfo();

            var provincePastWeek = _dataProvider.GetProvincePastInfo()
                ?.OrderByDescending(x => x.UnixTimestamp).Take(7).ToList();

            if (zonesDailyInfo == null || zonesDailyInfo.Count == 0 || provinceDailyInfo == null ||
                provincePastWeek == null || provincePastWeek.Count == 0)
            {
                return null;
            }

            var zonesRecoveryPhases = new List<ZoneRecoveryPhaseInfo>();

            foreach (var recoveryPhaseInfo in _dataProvider.GetZonesRecoveryPhaseInfo())
            {
                if (zonesDailyInfo.Any(x =>
                    x.HealthZone.Title.Equals(recoveryPhaseInfo.CommunityName, StringComparison.OrdinalIgnoreCase)))
                {
                    zonesRecoveryPhases.Add(recoveryPhaseInfo);
                }
            }

            const int extraRowCount = 3;
            var rows = new string[zonesDailyInfo.Count + extraRowCount][];

            var i = 0;

            rows[i++] = new[]
            {
                "Zone:",
                "Active",
                "New",
                "Recovery"
            };

            rows[i++] = new[]
            {
                "",
                "Cases:",
                "Cases:",
                "Phase:"
            };

            rows[i++] = new[]
            {
                "Overall",
                provinceDailyInfo.ActiveCases.ToString(),
                provinceDailyInfo.NewToday.ToString()
            };

            for (; i < rows.Length; i++)
            {
                var zone = zonesDailyInfo[i - extraRowCount];

                var recoveryPhase = zonesRecoveryPhases.FirstOrDefault(x => x.HealthZone == zone.HealthZone.ZoneNumber);

                rows[i] = new[]
                {
                    zone.HealthZone.Title,
                    zone.ActiveCases.ToString(),
                    zone.NewToday.ToString(),
                    recoveryPhase == null ? "Unknown" : recoveryPhase.RecoveryPhase
                };
            }

            var briefZoneContent = JoinRows(2, rows);

            var verboseProvinceContent = JoinRows(2,
                new[] { "Total Cases:", provinceDailyInfo.TotalCases.ToString() },
                new[] { "Travel Related:", provinceDailyInfo.TravelRelated.ToString() },
                new[] { "Close Contact:", provinceDailyInfo.CloseContact.ToString() },
                new[] { "Community Transmission:", provinceDailyInfo.CommTransmission.ToString() },
                new[] { "Under Investigation:", provinceDailyInfo.UnderInvestigation.ToString() });

            var embedBuilder = new EmbedBuilder();

            embedBuilder
                .WithTitle("New Brunswick COVID-19 Statistics")
                .WithUrl("https://experience.arcgis.com/experience/8eeb9a2052d641c996dba5de8f25a8aa")
                .WithColor(Color.Green)
                .AddField("Brief Data per Zone:", $"```{briefZoneContent}```")
                .AddField("Provincial Information:", $"```{verboseProvinceContent}```")
                .WithFooter("Bot by Stephen White - https://silk.one/", "https://static.silk.one/avatar.png")
                .WithTimestamp(GetDate(provinceDailyInfo.LastUpdate).ToLocalTime());

            return embedBuilder.Build();
        }
    }
}