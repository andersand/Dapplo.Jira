﻿// Copyright (c) Dapplo and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Dapplo.HttpExtensions;
using Dapplo.HttpExtensions.ContentConverter;
using Dapplo.HttpExtensions.Extensions;
using Dapplo.Log;
using Dapplo.Log.XUnit;
using Xunit.Abstractions;

namespace Dapplo.Jira.Tests
{
	/// <summary>
	///     Abstract base class for all tests
	/// </summary>
	public abstract class TestBase
	{
		protected static readonly LogSource Log = new LogSource();
		protected const string TestIssueKey = "DIT-1";

		// Test against a well known JIRA
        private static readonly Uri TestJiraUri = new Uri("https://greenshot.atlassian.net");

		/// <summary>
		///     Default test setup, can also take care of setting the authentication
		/// </summary>
		/// <param name="testOutputHelper"></param>
		/// <param name="doLogin"></param>
		protected TestBase(ITestOutputHelper testOutputHelper, bool doLogin = true)
		{

			var defaultJsonHttpContentConverterConfiguration = new DefaultJsonHttpContentConverterConfiguration
			{
				LogThreshold = 0
			};
			HttpBehaviour.Current.SetConfig(defaultJsonHttpContentConverterConfiguration);

			LogSettings.RegisterDefaultLogger<XUnitLogger>(LogLevels.Verbose, testOutputHelper);
			Client = JiraClient.Create(TestJiraUri);
			Username = Environment.GetEnvironmentVariable("jira_test_username");
			Password = Environment.GetEnvironmentVariable("jira_test_password");

			if (doLogin && !string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password))
			{
				Client.SetBasicAuthentication(Username, Password);
			}
		}

		/// <summary>
		///     The instance of the JiraClient
		/// </summary>
		protected IJiraClient Client { get; }

		protected string Username { get; }
		protected string Password { get; }
	}
}