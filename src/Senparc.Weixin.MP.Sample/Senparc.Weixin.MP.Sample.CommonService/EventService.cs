﻿/*----------------------------------------------------------------
    Copyright (C) 2017 Senparc
    
    文件名：EventService.cs
    文件功能描述：事件处理程序，此代码的简化MessageHandler方法已由/CustomerMessageHandler/CustomerMessageHandler_Event.cs完成
    
    
    创建标识：Senparc - 20150312
----------------------------------------------------------------*/

using System;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Senparc.Weixin.Exceptions;
using Senparc.Weixin.MP.Entities;
using Senparc.Weixin.MP.Helpers;
using Senparc.Weixin.MP.Sample.CommonService.TemplateMessage;

namespace Senparc.Weixin.MP.Sample.CommonService
{
    /// <summary>
    /// 全局微信事件有关的处理程序
    /// </summary>
    public class EventService
    {
        /// <summary>
        /// 微信MessageHandler事件处理，此代码的简化MessageHandler方法已由/CustomerMessageHandler/CustomerMessageHandler_Event.cs完成，
        /// 此方法不再更新
        /// </summary>
        /// <param name="requestMessage"></param>
        /// <returns></returns>
        public ResponseMessageBase GetResponseMessage(RequestMessageEventBase requestMessage)
        {
            ResponseMessageBase responseMessage = null;
            switch (requestMessage.Event)
            {
                case Event.ENTER:
                    {
                        var strongResponseMessage = requestMessage.CreateResponseMessage<ResponseMessageText>();
                        strongResponseMessage.Content = "您刚才发送了ENTER事件请求。";
                        responseMessage = strongResponseMessage;
                        break;
                    }
                case Event.LOCATION:
                    throw new Exception("暂不可用");
                    //break;
                case Event.subscribe://订阅
                    {
                        var strongResponseMessage = requestMessage.CreateResponseMessage<ResponseMessageText>();

                        //获取Senparc.Weixin.MP.dll版本信息
                        var fileVersionInfo = FileVersionInfo.GetVersionInfo(HttpContext.Current.Server.MapPath("~/bin/Senparc.Weixin.MP.dll"));
                        var version = fileVersionInfo.FileVersion;
                        strongResponseMessage.Content = string.Format(
                            "欢迎关注【Senparc.Weixin.MP 微信公众平台SDK】，当前运行版本：v{0}。\r\n您还可以发送【位置】【图片】【语音】信息，查看不同格式的回复。\r\nSDK官方地址：http://sdk.weixin.senparc.com",
                            version);
                        responseMessage = strongResponseMessage;
                        break;
                    }
                case Event.unsubscribe://退订
                    {
                        //实际上用户无法收到非订阅账号的消息，所以这里可以随便写。
                        //unsubscribe事件的意义在于及时删除网站应用中已经记录的OpenID绑定，消除冗余数据。
                        var strongResponseMessage = requestMessage.CreateResponseMessage<ResponseMessageText>();
                        strongResponseMessage.Content = "有空再来";
                        responseMessage = strongResponseMessage;
                        break;
                    }
                case Event.CLICK://菜单点击事件，根据自己需要修改
                    //这里的CLICK在此DEMO中不会被执行到，因为已经重写了OnEvent_ClickRequest
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return responseMessage;         
        }

        public void ConfigOnWeixinExceptionFunc(WeixinException ex)
        {
            try
            {
                Task.Factory.StartNew(async () =>
                {
                    var appId = ConfigurationManager.AppSettings["WeixinAppId"];
                    string openId = "olPjZjsXuQPJoV0HlruZkNzKc91E";//收到通知的管理员OpenId
                    var host = "A1 / AccessTokenOrAppId：" + (ex.AccessTokenOrAppId ?? "null");
                    string service = null;
                    string message = null;
                    var status = ex.GetType().Name;
                    var remark = "\r\n这是一条通过OnWeixinExceptionFunc事件发送的异步模板消息";
                    string url = "https://github.com/JeffreySu/WeiXinMPSDK/blob/24aca11630bf833f6a4b6d36dce80c5b171281d3/src/Senparc.Weixin.MP.Sample/Senparc.Weixin.MP.Sample/Global.asax.cs#L246";//需要点击打开的URL

                    var sendTemplateMessage = true;

                    if (ex is ErrorJsonResultException)
                    {
                        var jsonEx = (ErrorJsonResultException)ex;
                        service = jsonEx.Url;
                        message = jsonEx.Message;

                        //需要忽略的类型
                        var ignoreErrorCodes = new[]
                        {
                                ReturnCode.获取access_token时AppSecret错误或者access_token无效,
                                ReturnCode.template_id不正确,
                                ReturnCode.缺少access_token参数,
                                ReturnCode.api功能未授权,
                                ReturnCode.用户未授权该api,
                                ReturnCode.参数错误invalid_parameter,
                                ReturnCode.接口调用超过限制,
                                //其他更多可能的情况
                            };
                        if (ignoreErrorCodes.Contains(jsonEx.JsonResult.errcode))
                        {
                            sendTemplateMessage = false;//防止无限递归，这种请款那个下不发送消息
                        }

                        //TODO:防止更多的接口自身错误导致的无限递归。
                    }
                    else
                    {
                        if (ex.Message.StartsWith("openid:"))
                        {
                            openId = ex.Message.Split(':')[1];//发送给指定OpenId
                        }
                        service = "WeixinException";
                        message = ex.Message;
                    }

                    if (sendTemplateMessage)
                    {
                        int sleepSeconds = 3;
                        Thread.Sleep(sleepSeconds * 1000);
                        var data = new WeixinTemplate_ExceptionAlert(string.Format("微信发生异常（延时{0}秒）", sleepSeconds), host, service, status, message, remark);
                        var result = await Senparc.Weixin.MP.AdvancedAPIs.TemplateApi.SendTemplateMessageAsync(appId, openId, data.TemplateId,
                                url, data);
                    }
                });
            }
            catch (Exception e)
            {
                Senparc.Weixin.WeixinTrace.SendCustomLog("OnWeixinExceptionFunc过程错误", e.Message);
            }
        }
    }
}