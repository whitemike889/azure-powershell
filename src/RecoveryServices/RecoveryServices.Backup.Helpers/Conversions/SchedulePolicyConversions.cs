﻿// ----------------------------------------------------------------------------------
//
// Copyright Microsoft Corporation
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ----------------------------------------------------------------------------------

using Microsoft.Azure.Commands.RecoveryServices.Backup.Cmdlets.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using ServiceClientModel = Microsoft.Azure.Management.RecoveryServices.Backup.Models;

namespace Microsoft.Azure.Commands.RecoveryServices.Backup.Helpers
{
    /// <summary>
    /// Backup policy conversion helper
    /// </summary>
    public partial class PolicyHelpers
    {
        #region ServiceClientToPSObject conversions

        // <summary>
        /// Helper function to convert ps simple schedule policy from service response.
        /// </summary>
        public static SimpleSchedulePolicy GetPSSimpleSchedulePolicy(
            ServiceClientModel.SimpleSchedulePolicy serviceClientPolicy, string timeZone)
        {
            if (serviceClientPolicy == null)
            {
                return null;
            }

            SimpleSchedulePolicy psPolicy = new SimpleSchedulePolicy();

            psPolicy.ScheduleRunDays =
                HelperUtils.EnumListConverter<ServiceClientModel.DayOfWeek?, DayOfWeek>(
                    serviceClientPolicy.ScheduleRunDays);
            psPolicy.ScheduleRunFrequency = (ScheduleRunType)Enum.Parse(
                    typeof(ScheduleRunType), serviceClientPolicy.ScheduleRunFrequency.ToString());
            psPolicy.ScheduleRunTimes = ParseDateTimesToUTC(serviceClientPolicy.ScheduleRunTimes, timeZone);

            if (psPolicy.ScheduleRunFrequency == ScheduleRunType.Weekly)
            {
                int offset = psPolicy.ScheduleRunTimes[0].DayOfWeek.GetHashCode() -
                    serviceClientPolicy.ScheduleRunTimes[0].Value.DayOfWeek.GetHashCode();

                for (int index = 0; index < psPolicy.ScheduleRunDays.Count(); index++)
                {
                    if (offset == -1)
                    {
                        int value = psPolicy.ScheduleRunDays[index].GetHashCode() - 1;
                        if (value == -1)
                        {
                            value = 6;
                        }
                        psPolicy.ScheduleRunDays[index] = (DayOfWeek)value;
                    }
                    else if (offset == 1)
                    {
                        int value = psPolicy.ScheduleRunDays[index].GetHashCode() + 1;
                        if (value == 7)
                        {
                            value = 0;
                        }
                        psPolicy.ScheduleRunDays[index] = (DayOfWeek)value;
                    }
                }
            }
            
            if(psPolicy.ScheduleRunFrequency == ScheduleRunType.Hourly)
            {
                // multiple backups per day 
                psPolicy.ScheduleInterval = serviceClientPolicy.HourlySchedule.Interval;
                psPolicy.ScheduleWindowStartTime = serviceClientPolicy.HourlySchedule.ScheduleWindowStartTime;
                psPolicy.ScheduleWindowDuration = serviceClientPolicy.HourlySchedule.ScheduleWindowDuration;
                psPolicy.ScheduleRunTimeZone = timeZone;
                psPolicy.ScheduleRunDays = null;
                psPolicy.ScheduleRunTimes = null;
            }
            else
            {
                psPolicy.ScheduleInterval = null;
                psPolicy.ScheduleWindowStartTime = null;
                psPolicy.ScheduleWindowDuration = null;
                psPolicy.ScheduleRunTimeZone = timeZone;
            }

            // safe side validation
            if (psPolicy.ScheduleRunFrequency != ScheduleRunType.Hourly)
            {
                psPolicy.Validate();
            }            
                        
            return psPolicy;
        }

        // <summary>
        /// Helper function to convert ps log schedule policy from service response.
        /// </summary>
        public static LogSchedulePolicy GetPSLogSchedulePolicy(
            ServiceClientModel.LogSchedulePolicy serviceClientPolicy, string timeZone)
        {
            if (serviceClientPolicy == null)
            {
                return null;
            }

            LogSchedulePolicy psPolicy = new LogSchedulePolicy();
            psPolicy.ScheduleFrequencyInMins = serviceClientPolicy.ScheduleFrequencyInMins;

            // safe side validation
            psPolicy.Validate();

            return psPolicy;
        }

        #endregion

        #region PStoServiceClientObject conversions

        // <summary>
        /// Helper function to parse utc time from local time.
        /// </summary>
        public static List<DateTime> ParseDateTimesToUTC(IList<DateTime?> localTimes, string timeZone)
        {
            if (localTimes == null || localTimes.Count == 0)
            {
                return null;
            }

            List<DateTime> utcTimes = new List<DateTime>();
            DateTime temp;

            foreach (DateTime localTime in localTimes)
            {
                if (localTime == null)
                {
                    throw new ArgumentNullException("Policy date time object is null");
                }
                temp = localTime;
                if (!string.IsNullOrEmpty(timeZone))
                {
                    TimeZoneInfo timeZoneInfo = TimeZoneConverter.TZConvert.GetTimeZoneInfo(timeZone);
                    temp = DateTime.SpecifyKind(temp, DateTimeKind.Unspecified);
                    temp = TimeZoneInfo.ConvertTimeToUtc(temp, timeZoneInfo);
                }
                utcTimes.Add(temp);
            }

            return utcTimes;
        }

        // <summary>
        /// Helper function to convert service simple schedule policy from ps schedule policy.
        /// </summary>
        public static ServiceClientModel.SimpleSchedulePolicy GetServiceClientSimpleSchedulePolicy(
            SimpleSchedulePolicy psPolicy)
        {
            if (psPolicy == null)
            {
                return null;
            }

            ServiceClientModel.SimpleSchedulePolicy serviceClientPolicy = new ServiceClientModel.SimpleSchedulePolicy();
            serviceClientPolicy.ScheduleRunFrequency =
                ServiceClientHelpers.GetServiceClientScheduleRunType(
                    psPolicy.ScheduleRunFrequency);

            if (psPolicy.ScheduleRunFrequency == ScheduleRunType.Weekly)
            {
                serviceClientPolicy.ScheduleRunDays =
                    HelperUtils.EnumListConverter<DayOfWeek, ServiceClientModel.DayOfWeek>(
                        psPolicy.ScheduleRunDays).Cast<ServiceClientModel.DayOfWeek?>().ToList();
            }

            if (psPolicy.ScheduleRunFrequency != ScheduleRunType.Hourly)
            {
                serviceClientPolicy.ScheduleRunTimes = psPolicy.ScheduleRunTimes.ConvertAll(dateTime => (DateTime?)dateTime);
            }
            else
            {
                serviceClientPolicy.HourlySchedule = new ServiceClientModel.HourlySchedule();
                serviceClientPolicy.HourlySchedule.Interval = psPolicy.ScheduleInterval;
                serviceClientPolicy.HourlySchedule.ScheduleWindowDuration = psPolicy.ScheduleWindowDuration;
                serviceClientPolicy.HourlySchedule.ScheduleWindowStartTime = psPolicy.ScheduleWindowStartTime;
            }
            return serviceClientPolicy;
        }

        public static ServiceClientModel.LogSchedulePolicy GetServiceClientLogSchedulePolicy(
            LogSchedulePolicy psPolicy)
        {
            if (psPolicy == null)
            {
                return null;
            }

            ServiceClientModel.LogSchedulePolicy serviceClientPolicy = new ServiceClientModel.LogSchedulePolicy();
            serviceClientPolicy.ScheduleFrequencyInMins = psPolicy.ScheduleFrequencyInMins;
            return serviceClientPolicy;
        }

        // <summary>
        /// Helper function to get nullable date time list from date time list.
        /// </summary>
        public static List<DateTime?> GetNullableDateTimeListFromDateTimeList(
            IList<DateTime> localTimes)
        {
            if (localTimes == null || localTimes.Count == 0)
            {
                return null;
            }

            List<DateTime?> convertedTime = new List<DateTime?>();

            foreach (DateTime localTime in localTimes)
            {
                convertedTime.Add(localTime);
            }

            return convertedTime;
        }
        #endregion
    }
}
