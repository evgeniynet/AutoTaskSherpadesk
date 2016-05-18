//The MIT License (MIT)

//Copyright (c) <2013> <Jared L Jennings jared@jaredjennings.org>

//Permission is hereby granted, free of charge, to any person obtaining a copy
//of this software and associated documentation files (the "Software"), to deal
//in the Software without restriction, including without limitation the rights
//to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//copies of the Software, and to permit persons to whom the Software is
//furnished to do so, subject to the following conditions:

//The above copyright notice and this permission notice shall be included in
//all copies or substantial portions of the Software.

//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
//THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using AutoTask_Samples.StubAutoTask;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.Linq.Expressions;
using System.Reflection;

namespace AutoTask_Samples
{

	public enum XmlQueryOperator
	{
		Equals,
		LessThan,
		LessThanOrEqual,
		GreaterThan,
		GreaterThanOrEqual
	}

	public class XmlQueryBuilder<TEntity>
		where TEntity : class
	{
		private readonly XDocument _xDoc = new XDocument();
		private readonly XElement _queryNode = new XElement("query");

		public XmlQueryBuilder()
		{
			var rootNode = new XElement("queryxml");
			rootNode.SetAttributeValue("version", "1.0");

			var entityNode = new XElement("entity");
			entityNode.Value = typeof (TEntity).Name;

			rootNode.Add(entityNode);
			_xDoc.Add(rootNode);
		}

		public XmlQueryBuilder<TEntity> Where<TProperty>(Expression<Func<TEntity, TProperty>> property, XmlQueryOperator operation, string value)
		{
			var xCondition = new XElement("condition");
			var xField = new XElement("field");
			xField.Value = GetPropertyName(property);

			var xExpression = new XElement("expression");
			xExpression.SetAttributeValue("op", operation.ToString());
			xExpression.Value = value;

			xField.Add(xExpression);
			xCondition.Add(xField);

			_queryNode.Add(xCondition);
			return this;
		}

		public override string ToString()
		{
			var parent = _xDoc.Element("queryxml");
			if (parent == null) throw new InvalidOperationException("root node was not created.");

			parent.Add(_queryNode);
			return _xDoc.ToString();
		}

		private string GetPropertyName<TSource, TProperty>(Expression<Func<TSource, TProperty>> propertyLambda)
		{
			// credits http://stackoverflow.com/questions/671968/retrieving-property-name-from-lambda-expression/672212#672212 (modified to return property name)
			var type = typeof(TSource);

			var member = propertyLambda.Body as MemberExpression;
			if (member == null)
				throw new ArgumentException(string.Format("Expression '{0}' refers to a method, not a property.", propertyLambda));

			var propInfo = member.Member as PropertyInfo;
			if (propInfo == null)
				throw new ArgumentException(string.Format("Expression '{0}' refers to a field, not a property.", propertyLambda));

			if (type != propInfo.ReflectedType && !type.IsSubclassOf(propInfo.ReflectedType))
				throw new ArgumentException(string.Format("Expresion '{0}' refers to a property that is not from type {1}.", propertyLambda, type));

			return propInfo.Name;
		}
	}

	public class TestEntity
	{
		public string Test1 { get; set; }
		public int Test2 { get; set; }
		public DateTime Test3 { get; set; }
	}

        /// <summary>
        /// Demonstrates how to retrieve all active clients
        /// </summary>
        class QueryActiveClients
        {
                // the userID and password used to authenticate to AutoTask
                private static string auth_user_id = ""; // user@domain.com
                private static string auth_user_password = "";
                private static ATWSZoneInfo zoneInfo = null;

                public static void Main ()
                {

			var builder = new XmlQueryBuilder<TestEntity>()
				.Where(t => t.Test1, XmlQueryOperator.Equals, "abc")
				.Where(t => t.Test2, XmlQueryOperator.GreaterThan, "def")
				.Where(t => t.Test3, XmlQueryOperator.LessThanOrEqual, DateTime.Now.ToString());

			Console.WriteLine(builder.ToString());
			Console.ReadLine();

                        // demonstrates how to intellegently get the zone information.
                        // autotask will tell you which zone URL to use based on the userID
                        var client = new ATWSSoapClient ();
                        zoneInfo = client.getZoneInfo (auth_user_id);
                        Console.WriteLine ("ATWS Zone Info: \n\n"
                                + "URL = " + zoneInfo.URL);

                        // Create the binding.
                        // must use BasicHttpBinding instead of WSHttpBinding
                        // otherwise a "SOAP header Action was not understood." is thrown.
                        BasicHttpBinding myBinding = new BasicHttpBinding ();
                        myBinding.Security.Mode = BasicHttpSecurityMode.Transport;
                        myBinding.Security.Transport.ClientCredentialType = HttpClientCredentialType.Basic;

                        // Must set the size otherwise
                        //The maximum message size quota for incoming messages (65536) has been exceeded. To increase the quota, use the MaxReceivedMessageSize property on the appropriate binding element.
                        myBinding.MaxReceivedMessageSize = 2147483647;

                        // Create the endpoint address.
                        EndpointAddress ea = new EndpointAddress (zoneInfo.URL);

                        client = new ATWSSoapClient (myBinding, ea);
                        client.ClientCredentials.UserName.UserName = auth_user_id;
                        client.ClientCredentials.UserName.Password = auth_user_password;
                        
                        // query for any account. This should return all accounts since we are retreiving anything greater than 0.
                        StringBuilder sb = new StringBuilder ();
                        sb.Append ("<queryxml><entity>Account</entity>").Append (System.Environment.NewLine);
                        sb.Append ("<query><field>id<expression op=\"greaterthan\">0</expression></field></query>").Append (System.Environment.NewLine);
                        sb.Append ("</queryxml>").Append (System.Environment.NewLine);

                        // have no clue what this does.
                        AutotaskIntegrations at_integrations = new AutotaskIntegrations ();

                        // this example will not handle the 500 results limitation.
                        // Autotask only returns up to 500 results in a response. if there are more you must query again for the next 500.
                        var r = client.query (at_integrations, sb.ToString ());
                        Console.WriteLine ("response ReturnCode = " + r.ReturnCode);
                        if (r.ReturnCode == 1) {
                                if (r.EntityResults.Length > 0) {
                                        foreach (var item in r.EntityResults) {
                                                Account acct = (Account)item;
                                                Console.WriteLine ("Account Name = " + acct.AccountName);
                                                Console.WriteLine ("Account number = " + acct.AccountNumber);
                                        }
                                }
                        }
                        Console.ReadLine ();
                }

        }
}
