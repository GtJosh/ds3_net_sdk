﻿/*
 * ******************************************************************************
 *   Copyright 2014 Spectra Logic Corporation. All Rights Reserved.
 *   Licensed under the Apache License, Version 2.0 (the "License"). You may not use
 *   this file except in compliance with the License. A copy of the License is located at
 *
 *   http://www.apache.org/licenses/LICENSE-2.0
 *
 *   or in the "license" file accompanying this file.
 *   This file is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR
 *   CONDITIONS OF ANY KIND, either express or implied. See the License for the
 *   specific language governing permissions and limitations under the License.
 * ****************************************************************************
 */

using System.IO;
using System.Text;

using Ds3.Calls;
using Ds3.Models;
using Ds3.Runtime;
using Moq;

namespace TestDs3
{
    static class HelpersForTest
    {
        internal static string StringFromStream(Stream stream)
        {
            using (stream)
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }

        internal static Stream StringToStream(string responseString)
        {
            return new MemoryStream(Encoding.UTF8.GetBytes(responseString));
        }
    }
}