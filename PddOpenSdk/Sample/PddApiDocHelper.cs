using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Sample
{
    /// <summary>
    /// 拼多多接口获取帮助类
    /// </summary>
    public class PddApiDocHelper
    {
        readonly string CatApiUrl = "http://open-api.pinduoduo.com/pop/doc/list/by/cat";

        /// <summary>
        /// 获取分类下所有文档列表
        /// </summary>
        public async Task GetDocListByCatAsync(int catId = 1)
        {
            using (var hc = new HttpClient())
            {

                var requestContent = new StringContent(JsonConvert
                    .SerializeObject(new
                    {
                        id = catId
                    }), Encoding.UTF8, "application/json");

                var response = await hc.PostAsync(CatApiUrl, requestContent);
                var json = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonConvert.DeserializeObject<PddDocListModel>(json);
                    if (result.Success.Value)
                    {
                        string methodsContent = "";
                        var docList = result.Result.DocList;

                        // 取第一个作为类名
                        var className = docList?.FirstOrDefault()?.ScopeName;
                        if (className != null)
                        {
                            className = className.Split(".")[1] ?? "UnNamed";

                            foreach (var doc in docList)
                            {
                                methodsContent += await MethodBuildAsync(doc);
                            }

                            SaveRequestClass(className, methodsContent);
                        }

                    }
                }
            }
        }
        /// <summary>
        /// 保存接口请求类
        /// </summary>
        /// <param name="className"></param>
        protected void SaveRequestClass(string className, string classContent)
        {
            var currentPath = Directory.GetCurrentDirectory();
            var resultPath = Path.Combine(currentPath, "Services", "PddApiRequest");
            // 创建目录
            if (!Directory.Exists(resultPath))
            {
                Directory.CreateDirectory(resultPath);
            }
            string fileName = Function.ToTitleCase(className) + "ApiRequest";
            classContent = classContent.Replace("RootObject", fileName);

            string content = $@"namespace App.Services.PddApiRequest
{{
    public class {className} extends Request {{
        {classContent}
    }}
}}
";
            File.WriteAllText(Path.Combine(resultPath, fileName + ".cs"), content);

        }


        /// <summary>
        /// 生成接口调用方法
        /// </summary>
        /// <param name="doc"></param>
        public async Task<string> MethodBuildAsync(ApiDoc doc)
        {
            // 方法命名
            var scopeName = doc.ScopeName.Split('.');
            var methodName = Function.ToTitleCase(scopeName.Last());
            for (int i = 1; i < scopeName.Length - 1; i++)
            {
                methodName += Function.ToTitleCase(scopeName[i]);
            }
            // 方法参数

            var requestParamList = doc.RequestParamList.OrderByDescending(r => r.IsMust).ToList();
            string methodComment = $@"/// <summary>
/// {doc.ApiName}
/// </summary>
"; ;
            string methodParams = "";
            string dicData = "";

            foreach (var item in requestParamList)
            {
                string paramName = Function.ToTitleCase(item.ParamName.Replace("_", " "));

                dicData += $@"dic.Add(""{item.ParamName}"",{paramName});
";
                string paramComment = $@"/// <param name=""{paramName.Replace(" ", "")}"">{item.ParamDesc}</param>
";
                paramName = item.ParamType.ToLower() + " " + paramName.Replace(" ", "") + ",";
                methodParams += paramName;
                methodComment += paramComment;
            }
            methodParams = "(" + methodParams.Substring(0, methodParams.Length - 1) + ")";

            // TODO: 处理返回类型
            string returnType = methodName + "ApiResult";
            string jsonReturn = doc.CodeExample;
            // 调用接口将json转成csharpe class
            string classContent = await JsonToClass(jsonReturn);
            // 保存结果类
            SaveResultClass(returnType, classContent);

            return $@"{methodComment}public async Task<{returnType}> {methodName}Async{methodParams}
{{
    var dic = new Dictionary<string, string>();
    {dicData}    
    var result = Post<{returnType}>({doc.ScopeName},);
    return JsonConvert.DeserializeObject<{returnType}>(result);
}}";

        }


        /// <summary>
        /// json转class
        /// </summary>
        /// <param name="json"></param>
        /// <returns></returns>
        protected async Task<string> JsonToClass(string json)
        {
            if (string.IsNullOrEmpty(json)) return default;
            using (var hc = new HttpClient())
            {

                var data = new FormUrlEncodedContent(new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("json",json)
                });
                var response = await hc.PostAsync("http://json2csharp.com/Home/GenerateClasses", data);
                if (response.IsSuccessStatusCode)
                {
                    string jsonResonse = await response.Content.ReadAsStringAsync();
                    var jObject = JObject.Parse(jsonResonse);

                    var dic = JsonConvert.DeserializeObject<IDictionary<string, string>>(jObject["Classes"].ToString());
                    return string.Join("\n", dic.Values);
                }
                return default;
            }
        }

        /// <summary>
        /// 自动生成接口返回类
        /// </summary>
        protected void SaveResultClass(string className, string classContent)
        {
            var currentPath = Directory.GetCurrentDirectory();
            var resultPath = Path.Combine(currentPath, "Models", "PddApiResult");
            // 创建目录
            if (!Directory.Exists(resultPath))
            {
                Directory.CreateDirectory(resultPath);
            }
            string fileName = className + "ApiResult";
            classContent = classContent.Replace("RootObject", fileName);
            string content = $@"namespace App.Models.PddApiResult
{{
    {classContent}
}}
";
            File.WriteAllText(Path.Combine(resultPath, fileName + ".cs"), content);
        }
    }
}
