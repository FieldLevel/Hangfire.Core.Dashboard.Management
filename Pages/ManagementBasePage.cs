using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using Hangfire.Common;
using Hangfire.Core.Dashboard.Management.Metadata;
using Hangfire.Core.Dashboard.Management.Support;
using Hangfire.Dashboard;
using Hangfire.Dashboard.Pages;
using Hangfire.Server;
using Hangfire.States;
using Newtonsoft.Json;

namespace Hangfire.Core.Dashboard.Management.Pages
{
    public class ManagementBasePage : RazorPage
    {
        private readonly string pageTitle;
        private readonly string pageHeader;
        private readonly string section;
        
        protected internal ManagementBasePage(string pageTitle, string pageHeader, string section)
        {
            this.pageTitle = pageTitle;
            this.pageHeader = pageHeader;
            this.section = section;
        }

        protected virtual void Content()
        {
            var jobs = JobsHelper.Jobs.Where(j => j.ManagementPageSection.Contains(section)).OrderBy( job =>job.Type.ToString()).ToList();

            foreach (var jobMetadata in jobs)
            {

                var route = GetRoute(jobMetadata);

                var id = GetMethodName(jobMetadata);

                if (jobMetadata.MethodInfo.GetParameters().Length > 0)
                {

                    string inputs = string.Empty;

                    foreach (var parameterInfo in jobMetadata.MethodInfo.GetParameters())
                    {
                        if (parameterInfo.ParameterType == typeof(PerformContext) || parameterInfo.ParameterType == typeof(IJobCancellationToken))
                            continue;

                        DisplayDataAttribute displayInfo = null;
                        if (parameterInfo.GetCustomAttributes(true).OfType<DisplayDataAttribute>().Any())
                        {
                            displayInfo = parameterInfo.GetCustomAttribute<DisplayDataAttribute>();
                        }
                        
                        var myId = $"{id}_{parameterInfo.Name}";
                        if (parameterInfo.ParameterType == typeof(string))
                        {
                            inputs += InputTextbox(myId, displayInfo?.LabelText??parameterInfo.Name, displayInfo?.PlaceholderText??parameterInfo.Name);
                        }
                        else if (parameterInfo.ParameterType == typeof(System.Guid))
                        {
                            inputs += InputTextbox(myId, displayInfo?.LabelText ?? parameterInfo.Name, displayInfo?.PlaceholderText ?? parameterInfo.Name);
                        }
                        else if (parameterInfo.ParameterType == typeof(Decimal) || parameterInfo.ParameterType == typeof(Decimal?))
                        {
                            inputs += InputNumberbox(myId, displayInfo?.LabelText ?? parameterInfo.Name, displayInfo?.PlaceholderText ?? parameterInfo.Name);
                        }
                        else if (parameterInfo.ParameterType == typeof(float) || parameterInfo.ParameterType == typeof(float?))
                        {
                            inputs += InputNumberbox(myId, displayInfo?.LabelText ?? parameterInfo.Name, displayInfo?.PlaceholderText ?? parameterInfo.Name);
                        }
                        else if (parameterInfo.ParameterType == typeof(int))
                        {
                            inputs += InputNumberbox(myId, displayInfo?.LabelText ?? parameterInfo.Name, displayInfo?.PlaceholderText ?? parameterInfo.Name);
                        }
                        else if (parameterInfo.ParameterType == typeof(int?))
                        {
                            inputs += InputNumberbox(myId, displayInfo?.LabelText ?? parameterInfo.Name, displayInfo?.PlaceholderText ?? parameterInfo.Name);
                        }
                        else if (parameterInfo.ParameterType == typeof(DateTime))
                        {
                            inputs += InputDatebox(myId, displayInfo?.LabelText ?? parameterInfo.Name, displayInfo?.PlaceholderText ?? parameterInfo.Name);
                        }
                        else if (parameterInfo.ParameterType == typeof(DateTime?))
                        {
                            inputs += InputDatebox(myId, displayInfo?.LabelText ?? parameterInfo.Name, displayInfo?.PlaceholderText ?? parameterInfo.Name);
                        }
                        else if (parameterInfo.ParameterType == typeof(bool))
                        {
                            inputs += "<br/>" + InputCheckbox(myId, displayInfo?.LabelText ?? parameterInfo.Name, displayInfo?.PlaceholderText ?? parameterInfo.Name);
                        }
                        else if (parameterInfo.ParameterType.ToString().Contains("Enum"))
                        {
                            inputs += InputTextbox(myId, displayInfo?.LabelText ?? parameterInfo.Name, displayInfo?.PlaceholderText ?? parameterInfo.Name);
                        }
                        else if (parameterInfo.ParameterType.ToString().Contains("List"))
                        {
                            inputs += InputTextbox(myId, displayInfo?.LabelText ?? parameterInfo.Name, displayInfo?.PlaceholderText ?? parameterInfo.Name);
                        }
                        else if (parameterInfo.ParameterType.ToString().Contains("Dictionary"))
                        {
                            inputs += InputTextbox(myId, displayInfo?.LabelText ?? parameterInfo.Name, displayInfo?.PlaceholderText ?? parameterInfo.Name);
                        }
                        else
                        {
                            throw new NotImplementedException(parameterInfo.ParameterType.ToString() + " Converter Not Implemented");
                        }
                    }

                    Panel(id, jobMetadata.Type.Name + "." + jobMetadata.DisplayName, jobMetadata.Description, inputs, CreateButtons(route, "Enqueue", "enqueueing", id));

                }
                else
                {
                    Panel(id, jobMetadata.Type.Name + "." + jobMetadata.DisplayName, jobMetadata.Description, string.Empty, CreateButtons(route, "Enqueue", "enqueueing", id));

                }

            }

            WriteLiteral("\r\n<script src=\"");
            Write(Url.To($"/jsm"));
            WriteLiteral("\"></script>\r\n");

        }


        public static string GetRoute(JobMetadata jobMetadata)
        {
            return $"{ManagementPage.UrlRoute}/{jobMetadata.Type.Name.ToString().Replace(".","-")}/{jobMetadata.MethodInfo.Name}";
        }


        public static void Go(DashboardContext context, string message)
        {
            var responseObj = new { status = "test status", message };
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.WriteAsync(JsonConvert.SerializeObject(responseObj));
        }


        public static string GetMethodName(JobMetadata jobMetadata)
        {
            return $"{ jobMetadata.Type.Name.ToString() }-{ jobMetadata.MethodInfo.Name}";
        }

        public static void BuildApiRoutesAndHandlersForAllJobs(string section)
        {
            var jobs = JobsHelper.Jobs.Where(j => j.ManagementPageSection.Contains(section));


            foreach (var jobMetadata in jobs)
            {
                var route = GetRoute(jobMetadata);
                
                DashboardRoutes.Routes.Add(route, new CommandWithResponseDispatcher(async (context) => 
                {
                    var par = new List<object>();

                    var methodName = GetMethodName(jobMetadata);
                    var schedule = Task.Run(() => context.Request.GetFormValuesAsync($"{methodName}_schedule")).Result.FirstOrDefault();
                    var cron = Task.Run(() => context.Request.GetFormValuesAsync($"{methodName}_cron")).Result.FirstOrDefault();

                    var currentParam = "";
                    try
                    {
                        
                        foreach (var parameterInfo in jobMetadata.MethodInfo.GetParameters())
                        {
                            currentParam = parameterInfo.Name;

                            if (parameterInfo.ParameterType == typeof(PerformContext) || parameterInfo.ParameterType == typeof(IJobCancellationToken))
                            {
                                par.Add(null);
                                continue;
                            }


                            var variable = $"{methodName}_{parameterInfo.Name}";
                            if (parameterInfo.ParameterType == typeof(DateTime) || parameterInfo.ParameterType == typeof(DateTime?))
                            {
                                variable = $"{variable}_datetimepicker";
                            }

                            var t = Task.Run(() => context.Request.GetFormValuesAsync(variable)).Result;

                            object item = null;
                            var formInput = t.FirstOrDefault();
                            
                            if (parameterInfo.ParameterType == typeof(string))
                            {
                                item = formInput;
                            }
                            else if (parameterInfo.ParameterType == typeof(System.Guid))
                            {
                                if (formInput != null) item = Guid.Parse(formInput);
                            }
                            else if (parameterInfo.ParameterType == typeof(Decimal))
                            {
                                if (formInput != null) item = Decimal.Parse(formInput);
                            }
                            else if (parameterInfo.ParameterType == typeof(Decimal?))
                            {
                                if (formInput != null) item = Decimal.Parse(formInput);
                            }
                            else if (parameterInfo.ParameterType == typeof(float))
                            {
                                if (formInput != null) item = float.Parse(formInput);
                            }
                            else if (parameterInfo.ParameterType == typeof(float?))
                            {
                                if (formInput != null) item = float.Parse(formInput);
                            }
                            else if (parameterInfo.ParameterType == typeof(int))
                            {
                                if (formInput != null) item = int.Parse(formInput);
                            }
                            else if (parameterInfo.ParameterType == typeof(int?))
                            {
                                item = (formInput == null || String.IsNullOrEmpty(formInput)) ? (int?)null : int.Parse(formInput);
                            }
                            else if (parameterInfo.ParameterType == typeof(DateTime))
                            {
                                item = formInput == null ? DateTime.MinValue : DateTime.Parse(formInput);
                            }
                            else if (parameterInfo.ParameterType == typeof(DateTime?))
                            {
                                item = formInput == null ? (DateTime?)null : DateTime.Parse(formInput);
                            }
                            else if (parameterInfo.ParameterType == typeof(bool))
                            {
                                item = formInput == "on";
                            }
                            else if (parameterInfo.ParameterType == typeof(Dictionary<string,object>))
                            {
                                item = formInput == null ? (Dictionary<string, object>)null : JsonConvert.DeserializeObject<Dictionary<string, object>>(formInput);
                            }
                            else if (parameterInfo.ParameterType == typeof(List<int>))
                            {
                                item = formInput == null ? (List<int>)null : formInput.Split(',').Select(int.Parse).ToList();
                            }
                            else if (parameterInfo.ParameterType == typeof(List<string>))
                            {
                                item = formInput == null ? (List<string>)null : formInput.Split(',').ToList();
                            }
                            else if (parameterInfo.ParameterType.ToString().Contains("Enum"))
                            {
                                if (formInput != null)
                                {
                                    try
                                    {
                                        item = Enum.Parse(parameterInfo.ParameterType, formInput);
                                    }
                                    catch (Exception e)
                                    {
                                        throw new ArgumentException("Specified enum type could not be found", parameterInfo.ParameterType.ToString());
                                    }
                                }
                            }
                            else
                            {
                                throw new NotImplementedException(parameterInfo.ParameterType.ToString() + " Converter Not Implemented");
                            }

                            par.Add(item);

                        }
                    }
                    catch (Exception e)
                    {
                        var responseObj = new { status = $"failed parsing param ({currentParam})", message = e.Message };
                        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                        await context.Response.WriteAsync(JsonConvert.SerializeObject(responseObj));
                        return false;
                    }

                    try
                    {
                        var job = new Job(jobMetadata.Type, jobMetadata.MethodInfo, par.ToArray());
                        var client = new BackgroundJobClient(context.Storage);
                        string jobLink = null;
                        string jobId = null;

                        if (!string.IsNullOrEmpty(schedule))
                        {
                            try
                            {
                                var minutes = int.Parse(schedule);
                                jobId = client.Create(job, new ScheduledState(new TimeSpan(0, 0, minutes, 0)));
                                jobLink = new UrlHelper(context).JobDetails(jobId);
                            }
                            catch (Exception e)
                            {
                                var responseObj = new { status = "failed creating recurring job", message = e.Message };
                                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                                await context.Response.WriteAsync(JsonConvert.SerializeObject(responseObj));
                                return false;
                            }
                            
                        }
                        else if (!string.IsNullOrEmpty(cron))
                        {
                            var manager = new RecurringJobManager(context.Storage);
                            try
                            {
                                var recurringJobUnqiueId = String.Format(jobMetadata.DisplayName, job.Args.ToArray());
                                manager.AddOrUpdate(recurringJobUnqiueId, job, cron, TimeZoneInfo.Utc);
                                jobLink = new UrlHelper(context).To("/recurring");
                            }
                            catch (Exception e)
                            {
                                var responseObj = new { status = "failed creating recurring job", message = e.Message };
                                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                                await context.Response.WriteAsync(JsonConvert.SerializeObject(responseObj));
                                return false;
                            }
                        }
                        else
                        {
                            jobId = client.Create(job, new EnqueuedState(jobMetadata.Queue));
                            jobLink = new UrlHelper(context).JobDetails(jobId);
                        }

                        if (!string.IsNullOrEmpty(jobLink))
                        {
                            var responseObj = new { jobLink = jobLink };
                            var json = JsonConvert.SerializeObject(responseObj);
                            await context.Response.WriteAsync(json);
                            return true;
                        }
                        else
                        {
                            var responseObj = new { status = "fail" };
                            context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                            await context.Response.WriteAsync(JsonConvert.SerializeObject(responseObj));
                            return false;
                        }
                    }
                    catch (Exception e)
                    {
                        var responseObj = new { status = "fail", message = e.Message };
                        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                        await context.Response.WriteAsync(JsonConvert.SerializeObject(responseObj));
                        return false;
                    }
                    
                    
                }));
            }
        }

        public override void Execute()
        {
            WriteLiteral("\r\n");
            Layout = new LayoutPage(pageTitle);

            WriteLiteral("<div class=\"row\">\r\n");
            WriteLiteral("<div class=\"col-md-3\">\r\n");

            Write(Html.RenderPartial(new CustomSidebarMenu(ManagementSidebarMenu.Items)));

            WriteLiteral("</div>\r\n");
            WriteLiteral("<div class=\"col-md-9\">\r\n");
            WriteLiteral("<h1 class=\"page-header\">\r\n");
            Write(pageHeader);
            WriteLiteral("</h1>\r\n");

            Content();

            WriteLiteral("\r\n</div>\r\n");
            WriteLiteral("\r\n</div>\r\n");
        }

        protected void Panel(string id, string heading, string description, string content, string buttons)
        {
            WriteLiteral($@"<div class=""panel panel-info js-management"">
                              <div class=""panel-heading"">{heading}</div>
                              <div class=""panel-body"">
                                <p>{description}</p>
                              </div>
                              <div class=""panel-body"">");

            if (!string.IsNullOrEmpty(content))
            {
                WriteLiteral($@"<div class=""well""> 
                                    { content}
                                </div>      
                                                     
                              ");
            }

            WriteLiteral($@"<div id=""{id}_error"" ></div>  <div id=""{id}_success"" ></div>  
                            </div>
                            <div class=""panel-footer clearfix "">
                                <div class=""pull-right"">
                                    { buttons}
                                </div>
                              </div>
                            </div>");
        }

        protected string CreateButtons(string url, string text, string loadingText, string id)
        {
            return $@" 

                        <div class=""col-sm-2 pull-right"">
                            <button class=""js-management-input-commands btn btn-sm btn-success"" 
                                    data-url=""{Url.To(url)}"" data-loading-text=""{loadingText}"" input-id=""{id}""> 
                                <span class=""glyphicon glyphicon-play-circle""></span>
                                &nbsp;Enqueue
                            </button>
                        </div>
                        <div class=""btn-group col-3 pull-right"">
                            <button type=""button"" class=""btn btn-info btn-sm dropdown-toggle"" data-toggle=""dropdown"" aria-haspopup=""true"" aria-expanded=""false"">
                                Schedule &nbsp;
                                <span class=""caret""></span>
                            </button>
                                
                            <ul class=""dropdown-menu"">
                                <li><a href=""#"" class=""js-management-input-commands"" input-id=""{id}"" schedule=""5""  
                                    data-url=""{Url.To(url)}"" data-loading-text=""{loadingText}"">5 min</a></li>
                                <li><a href=""#"" class=""js-management-input-commands"" input-id=""{id}"" schedule=""10""
                                     data-url=""{Url.To(url)}"" data-loading-text=""{loadingText}"">10 min</a></li>
                                <li><a href=""#"" class=""js-management-input-commands"" input-id=""{id}"" schedule=""15""
                                     data-url=""{Url.To(url)}"" data-loading-text=""{loadingText}"">15 min</a></li>
                                <li><a href=""#"" class=""js-management-input-commands"" input-id=""{id}"" schedule=""30""
                                     data-url=""{Url.To(url)}"" data-loading-text=""{loadingText}"">30 min</a></li>
                                <li><a href=""#"" class=""js-management-input-commands"" input-id=""{id}"" schedule=""60""
                                     data-url=""{Url.To(url)}"" data-loading-text=""{loadingText}"">60 min</a></li>
                                <li><a href=""#"" class=""js-management-input-commands"" input-id=""{id}"" schedule=""120""
                                     data-url=""{Url.To(url)}"" data-loading-text=""{loadingText}"">2 Hours</a></li>
                                <li><a href=""#"" class=""js-management-input-commands"" input-id=""{id}"" schedule=""180""
                                     data-url=""{Url.To(url)}"" data-loading-text=""{loadingText}"">3 Hours</a></li>
                                <li><a href=""#"" class=""js-management-input-commands"" input-id=""{id}"" schedule=""240""
                                     data-url=""{Url.To(url)}"" data-loading-text=""{loadingText}"">4 Hours</a></li>
                                <li><a href=""#"" class=""js-management-input-commands"" input-id=""{id}"" schedule=""300""
                                     data-url=""{Url.To(url)}"" data-loading-text=""{loadingText}"">5 Hours</a></li>
                                <li><a href=""#"" class=""js-management-input-commands"" input-id=""{id}"" schedule=""360""
                                     data-url=""{Url.To(url)}"" data-loading-text=""{loadingText}"">6 Hours</a></li>
                                <li><a href=""#"" class=""js-management-input-commands"" input-id=""{id}"" schedule=""420""
                                     data-url=""{Url.To(url)}"" data-loading-text=""{loadingText}"">7 Hours</a></li>
                                <li><a href=""#"" class=""js-management-input-commands"" input-id=""{id}"" schedule=""480""
                                     data-url=""{Url.To(url)}"" data-loading-text=""{loadingText}"">8 Hours</a></li>
                                <li><a href=""#"" class=""js-management-input-commands"" input-id=""{id}"" schedule=""540""
                                     data-url=""{Url.To(url)}"" data-loading-text=""{loadingText}"">9 Hours</a></li>
                                <li><a href=""#"" class=""js-management-input-commands"" input-id=""{id}"" schedule=""600""
                                     data-url=""{Url.To(url)}"" data-loading-text=""{loadingText}"">10 Hours</a></li>
                                <li><a href=""#"" class=""js-management-input-commands"" input-id=""{id}"" schedule=""660""
                                     data-url=""{Url.To(url)}"" data-loading-text=""{loadingText}"">11 Hours</a></li>
                                <li><a href=""#"" class=""js-management-input-commands"" input-id=""{id}"" schedule=""720""
                                     data-url=""{Url.To(url)}"" data-loading-text=""{loadingText}"">12 Hours</a></li>
                                <li><a href=""#"" class=""js-management-input-commands"" input-id=""{id}"" schedule=""780""
                                     data-url=""{Url.To(url)}"" data-loading-text=""{loadingText}"">13 Hours</a></li>
                                <li><a href=""#"" class=""js-management-input-commands"" input-id=""{id}"" schedule=""840""
                                     data-url=""{Url.To(url)}"" data-loading-text=""{loadingText}"">14 Hours</a></li>
                                <li><a href=""#"" class=""js-management-input-commands"" input-id=""{id}"" schedule=""900""
                                     data-url=""{Url.To(url)}"" data-loading-text=""{loadingText}"">15 Hours</a></li>
                                <li><a href=""#"" class=""js-management-input-commands"" input-id=""{id}"" schedule=""960""
                                     data-url=""{Url.To(url)}"" data-loading-text=""{loadingText}"">16 Hours</a></li>
                                <li><a href=""#"" class=""js-management-input-commands"" input-id=""{id}"" schedule=""1020""
                                     data-url=""{Url.To(url)}"" data-loading-text=""{loadingText}"">17 Hours</a></li>
                                <li><a href=""#"" class=""js-management-input-commands"" input-id=""{id}"" schedule=""1080""
                                     data-url=""{Url.To(url)}"" data-loading-text=""{loadingText}"">18 Hours</a></li>
                                <li><a href=""#"" class=""js-management-input-commands"" input-id=""{id}"" schedule=""1140""
                                     data-url=""{Url.To(url)}"" data-loading-text=""{loadingText}"">19 Hours</a></li>
                                <li><a href=""#"" class=""js-management-input-commands"" input-id=""{id}"" schedule=""1200""
                                     data-url=""{Url.To(url)}"" data-loading-text=""{loadingText}"">20 Hours</a></li>
                                <li><a href=""#"" class=""js-management-input-commands"" input-id=""{id}"" schedule=""1260""
                                     data-url=""{Url.To(url)}"" data-loading-text=""{loadingText}"">21 Hours</a></li>
                                <li><a href=""#"" class=""js-management-input-commands"" input-id=""{id}"" schedule=""1320""
                                     data-url=""{Url.To(url)}"" data-loading-text=""{loadingText}"">22 Hours</a></li>
                                <li><a href=""#"" class=""js-management-input-commands"" input-id=""{id}"" schedule=""1380""
                                     data-url=""{Url.To(url)}"" data-loading-text=""{loadingText}"">23 Hours</a></li>
                                <li><a href=""#"" class=""js-management-input-commands"" input-id=""{id}"" schedule=""1440""
                                     data-url=""{Url.To(url)}"" data-loading-text=""{loadingText}"">24 Hours</a></li>


                            </ul>
                        </div>

                        <div class=""col-sm-5 pull-right"">
                            <div class=""input-group input-group-sm"">
                                <input type=""text"" class=""form-control"" placeholder=""Enter a cron expression * * * * *"" id=""{id}_cron"">
                                <span class=""input-group-btn "">
                                <button class=""btn btn-default btn-sm btn-warning js-management-input-commands"" type=""button"" input-id=""{id}""
                                     data-url=""{Url.To(url)}"" data-loading-text=""{loadingText}"">
                                    <span class=""glyphicon glyphicon-repeat""></span>
                                    &nbsp;Add Recurring</button>
                                </span>
                            </div>
                        </div>
                       ";
        }

        private string Input(string id, string labelText, string placeholderText, string inputtype)
        {
            return $@"
                    <div class=""form-group"">
                        <label for=""{id}"" class=""control-label"">{labelText}</label>
                        <input type=""{inputtype}"" placeholder=""{placeholderText}"" id=""{id}"" >
                    </div>
            ";
        }

        protected string InputTextbox(string id, string labelText, string placeholderText)
        {
            return Input(id, labelText, placeholderText, "text");
        }
        protected string InputNumberbox(string id, string labelText, string placeholderText)
        {
            return Input(id, labelText, placeholderText, "number");
        }

        protected string InputDatebox(string id, string labelText, string placeholderText)
        {
            return $@"
                    <div class=""form-group"">
                        <label for=""{id}"" class=""control-label"">{labelText}</label>
                        <div class='input-group date' id='{id}_datetimepicker'>
                            <input type='text' class=""form-control"" placeholder=""{placeholderText}"" />
                            <span class=""input-group-addon"">
                                <span class=""glyphicon glyphicon-calendar""></span>
                            </span>
                        </div>
                    </div>";

        }

        protected string InputCheckbox(string id, string labelText, string placeholderText)
        {
            return $@"
                        <div class=""form-group"">
                            <div class=""checkbox"">
                              <label>
                                <input type=""checkbox"" id=""{id}"">
                                {labelText}
                              </label>                             
                            </div>
                        </div>
            ";
        }

    }
}
