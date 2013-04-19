﻿// Copyright (c) 2012, Event Store LLP
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
// 
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using EventStore.ClientAPI;
using EventStore.Core.Tests.ClientAPI.Helpers;
using NUnit.Framework;
using EventStore.Core.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EventStore.Core.Tests.Http.Users
{
    internal abstract class HttpBehaviorSpecification : SpecificationWithDirectoryPerTestFixture
    {
        protected MiniNode _node;
        protected EventStoreConnection _connection;
        protected HttpWebResponse _lastResponse;
        protected string _lastResponseBody;
        protected JsonException _lastJsonException;

        [TestFixtureSetUp]
        public override void TestFixtureSetUp()
        {
            base.TestFixtureSetUp();
            _node = new MiniNode(PathName);
            _node.Start();

            _connection = TestConnection.Create();
            _connection.Connect(_node.TcpEndPoint);
        }

        [TestFixtureTearDown]
        public override void TestFixtureTearDown()
        {
            _connection.Close();
            _node.Shutdown();
            base.TestFixtureTearDown();
        }

        protected HttpWebRequest CreateRequest(string path, string method, string contentType)
        {
            var httpEndPoint = _node.HttpEndPoint;
            var httpWebRequest =
                (HttpWebRequest)
                WebRequest.Create(
                    new UriBuilder("http", httpEndPoint.Address.ToString(), httpEndPoint.Port, path).Uri);
            httpWebRequest.Method = method;
            httpWebRequest.ContentType = contentType;
            return httpWebRequest;
        }

        protected HttpWebResponse MakeJsonPost<T>(string users, T body)
        {
            var request = CreateJsonPostRequest(
                users, body);
            var httpWebResponse = (HttpWebResponse) request.GetResponse();
            return httpWebResponse;
        }

        protected T GetJson<T>(string path)
        {
            var request = CreateRequest(path, "GET", null);
            _lastResponse = (HttpWebResponse) request.GetResponse();
            var memoryStream = new MemoryStream();
            _lastResponse.GetResponseStream().CopyTo(memoryStream);
            var bytes = memoryStream.ToArray();
            _lastResponseBody = Encoding.UTF8.GetString(bytes);
            try
            {
                return _lastResponseBody.ParseJson<T>();
            }
            catch (JsonException ex)
            {
                _lastJsonException = ex;
                return default(T);
            }
        }

        private HttpWebRequest CreateJsonPostRequest<T>(string path, T body)
        {
            var request = CreateRequest(path, "POST", "application/json");
            request.GetRequestStream().WriteJson(body);
            return request;
        }

        [SetUp]
        public void SetUp()
        {
            Given();
            When();
        }

        protected abstract void Given();
        protected abstract void When();

        protected void AssertJObject(JObject expected, JObject response, string path)
        {
            foreach (KeyValuePair<string, JToken> v in expected)
            {
                JToken vv;
                if (!response.TryGetValue(v.Key, out vv))
                {
                    Assert.Fail(string.Format("{0}/{1} not found", path, v.Key));
                }
                else
                {
                    Assert.AreEqual(
                        v.Value.Type, vv.Type, "{0}/{1} type is {2}, but {3} is expected", path, v.Key, vv.Type,
                        v.Value.Type);
                    if (v.Value.Type == JTokenType.Object)
                    {
                        AssertJObject(v.Value as JObject, vv as JObject, path + "/" + v.Key);
                    }
                    else if (v.Value.Type == JTokenType.Array)
                    {
                        AssertJArray(v.Value as JArray, vv as JArray, path + "/" + v.Key);
                    }
                    else
                    {
                        Assert.AreEqual(
                            v.Value, vv, "{0}/{1} value is '{2}' but '{3}' is expected", path, v.Key, vv, v.Value);
                    }
                }
            }
        }

        private void AssertJArray(JArray expected, JArray response, string path)
        {
            for (int index = 0; index < expected.Count; index++)
            {
                JToken v = expected[index];
                JToken vv = response[index];
                Assert.AreEqual(
                    v.Type, vv.Type, "{0}/{1} type is {2}, but {3} is expected", path, index, vv.Type,
                    v.Type);
                if (v.Type == JTokenType.Object)
                {
                    AssertJObject(v as JObject, vv as JObject, path + "/" + index);
                }
                else if (v.Type == JTokenType.Array)
                {
                    AssertJArray(v as JArray, vv as JArray, path + "/" + index);
                }
                else
                {
                    Assert.AreEqual(
                        v, vv, "{0}/{1} value is '{2}' but '{3}' is expected", path, index,
                        vv, v);
                }
            }
        }
    }
}
