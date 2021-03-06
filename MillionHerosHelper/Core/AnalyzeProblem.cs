﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Threading;

namespace MillionHerosHelper
{
    static class AnalyzeProblem
    {
        private static string problemData;
        private static int problemCnt;
        private static int[] answerCnt;
        private static int[] problemAndAnswerCnt;

        public static AnalyzeResult Analyze(string problem, string[] answerArr)
        {
            bool oppose = Regex.IsMatch(problem, "不是|不属于|不包括|不可以|不包含|不需要|错误|没有|不会");//是否存在否定关键词

            //移除部分干扰搜索的关键字
            problem = RemoveUselessInfoAndPrivative(problem);

            #region 多线程获取信息
            Thread[] workThread = new Thread[1 + answerArr.Length * 2];//工作线程

            workThread[0] = new Thread(new ParameterizedThreadStart(WorkThread));
            WorkArgs args;

            args.Type = TaskType.GetProblemData;
            args.Index = -1;
            args.Text = problem;
            workThread[0].Start(args);

            answerCnt = new int[answerArr.Length];
            for (int i = 0; i < answerArr.Length; i++) 
            {
                args.Type = TaskType.AnswerCnt;
                args.Index = i;
                args.Text = answerArr[i];
                workThread[i + 1] = new Thread(new ParameterizedThreadStart(WorkThread));
                workThread[i + 1].Start(args);
            }

            problemAndAnswerCnt = new int[answerArr.Length];
            for (int i = 0; i < answerArr.Length; i++) 
            {
                args.Type = TaskType.ProblemAndAnswerCnt;
                args.Index = i;
                args.Text = problem + " " + "\"" + answerArr[i] + "\"";
                workThread[i + 1 + answerArr.Length] = new Thread(new ParameterizedThreadStart(WorkThread));
                workThread[i + 1 + answerArr.Length].Start(args);
            }

            while(true)//等待所有线程执行完毕
            {
                bool allExited = true;
                for (int i = 0; i < workThread.Length; i++) 
                {
                    if(workThread[i].IsAlive)
                    {
                        allExited = false;
                        break;
                    }
                }

                if (allExited)
                    break;
                else
                    Thread.Sleep(50);
            }
            #endregion

            //计算权值
            double[] pmiRank = CalculateRank.CalculatePMIRank(problemCnt, answerCnt, problemAndAnswerCnt);
            double[] cntRank = CalculateRank.CalculateCountRank(problem, answerArr, pmiRank, problemData);
            double[] sumRank = new double[answerArr.Length];

            double pmiAddCnt = 0;
            for (int i = 0; i < answerArr.Length; i++)
            {
                pmiAddCnt += pmiRank[i] + cntRank[i];
            }

            int trueAnswerIndex = SearchTureAnswer(problemData, answerArr);
            if (trueAnswerIndex != -1)
            {
                Debug.WriteLine("测试:" + trueAnswerIndex + "|" + pmiAddCnt);
                sumRank[trueAnswerIndex] = pmiAddCnt;
            }

            int maxIndex = 0;
            int minIndex = 0;
            for (int i = 0; i < answerArr.Length; i++)
            {
                sumRank[i] += pmiRank[i] + cntRank[i];
                if (sumRank[i] > sumRank[maxIndex])
                {
                    maxIndex = i;
                }

                if (sumRank[i] < sumRank[minIndex])
                {
                    minIndex = i;
                }
            }

            double probabilitySum = 0;

            for (int i = 0; i < cntRank.Length; i++) 
            {
                probabilitySum += sumRank[i];
            }
            //计算概率
            int[] probability = new int[answerArr.Length];
            for (int i = 0; i < answerArr.Length; i++)
            {
                probability[i] = (int)(sumRank[i] / probabilitySum * 100);
            }

            AnalyzeResult ar;
            ar.CntRank = cntRank;
            ar.PMIRank = pmiRank;
            ar.Index = maxIndex;
            if (oppose) 
            {
                ar.Index = minIndex;
            }
            ar.SumRank = sumRank;
            ar.Probability = probability;
            ar.Oppose = oppose;
            return ar;
        }

        private static void WorkThread(object arg)
        {
            WorkArgs args = (WorkArgs)arg;
            if(args.Type == TaskType.GetProblemData)
            {
                problemCnt = SearchEngine.StatisticsKeyword(args.Text, out problemData);
            }
            else
            {
                int[] arr = (args.Type == TaskType.AnswerCnt) ? answerCnt : problemAndAnswerCnt;
                arr[args.Index] = SearchEngine.StatisticsKeyword(args.Text);
            }
        }

        /// <summary>
        /// 算法修正,移除无用信息
        /// </summary>
        public static string RemoveUselessInfoAndPrivative(string str)
        {
            str = RemoveUselessInfo(str);
            //"不是", "不属于", "不包括", "不可以", "不包含", "不需要", "错误", "没有", "不会" 
            Dictionary<string, string> dic = new Dictionary<string, string>();
            dic.Add("不是", "是");
            dic.Add("不属于", "属于");
            dic.Add("不包括", "包括");
            dic.Add("不可以", "可以");
            dic.Add("不包含", "包含");
            dic.Add("不需要", "需要");
            dic.Add("错误", "正确");
            dic.Add("没有", "有");
            dic.Add("不会", "会");
            foreach(string key in dic.Keys)
            {
                str = str.Replace(key, dic[key]);
            }
            Debug.WriteLine(str);
            return str;
        }

        public static string RemoveUselessInfo(string str)
        {

            if (str.Length >= 38)
            {
                if (str.Contains("请问"))
                {
                    int p = str.IndexOf("请问");
                    str = str.Substring(p, str.Length - p);
                }
            }

            string[] dic = new string[] { "“", "”", "\"", "以下", "下列", "哪项", "选项" };
            string res = str;
            foreach (string key in dic)
            {
                res = res.Replace(key, "");
            }
            res = res.Replace("？", "?");
            return res;
        }

        public static string RemoveABC(string str)
        {
            string[] dic = new string[] { "A.", "B.", "C.", "A", "B", "C"};
            string res = str;
            foreach (string key in dic)
            {
                if(res.StartsWith(key))
                {
                    int len = key.Length;
                    res = res.Substring(len, res.Length - len);
                }
            }
            return res;
        }

        /// <summary>
        /// 依赖百度百科、图谱、计算器等, 寻找是否存在确定的答案
        /// </summary>
        /// <param name="data">整个搜索页面</param>
        /// <param name="answerArr">选项数组</param>
        /// <returns>找到返回下标,未找到返回-1</returns>
        public static int SearchTureAnswer(string data, string[] answerArr)
        {
            int p;

            #region 百度计算器
            p = data.IndexOf("mu=\"http://open.baidu.com/static/calculator/calculator.html\"");
            if (p != -1) //百度计算器 
            {
                if (CountItemsBeforeP(data, p) < 2)//确保词条在前两项
                {
                    //定位
                    p = data.IndexOf("<p class=\"op_new_val_screen_result\">", p);
                    if (p != -1)
                    {
                        int startP = data.IndexOf("<span>", p);
                        if (startP != -1)
                        {
                            int endP = data.IndexOf("</span>", startP);
                            if (endP != -1)
                            {
                                string ans = data.Substring(startP + "<span>".Length, endP - (startP + "<span>".Length));
                                ans = ans.Replace(" ", "");
                                int existCnt = 0;//正确答案存在个数
                                int index = 0;//正确答案下标
                                for (int i = 0; i < answerArr.Length; i++)
                                {
                                    if (ans.Contains(answerArr[i]))
                                    {
                                        existCnt++;
                                        index = i;
                                    }
                                }

                                if (existCnt == 1) //存在个数，只有一个才能确保是正确选项
                                {
                                    return index;
                                }
                            }
                        }
                    }
                }
            }
            #endregion

            #region 百度汉语
            p = data.IndexOf("<div class=\"op_exactqa_detail_s_answer\">");
            if (p != -1)
            {
                if (CountItemsBeforeP(data, p) < 2)//确保词条在前两项
                {
                    const string startStr = "target=\"_blank\">";
                    const string endStr = "</a></span>";

                    int startP = data.IndexOf(startStr, p);
                    if (startP != -1) 
                    {
                        int endP = data.IndexOf(endStr, startP);
                        if (endP != -1)
                        {
                            string ans = data.Substring(startP + startStr.Length, endP - (startP + endStr.Length));
                            int existCnt = 0;//正确答案存在个数
                            int index = 0;//正确答案下标
                            for (int i = 0; i < answerArr.Length; i++)
                            {
                                if (ans.Contains(answerArr[i]))
                                {
                                    existCnt++;
                                    index = i;
                                }
                            }

                            if (existCnt == 1) //存在个数，只有一个才能确保是正确选项
                            {
                                return index;
                            }
                        }
                    }
                }
            }
            #endregion


            #region 百度知识图谱

            p = data.IndexOf("data-compress=\"off\">");
            if (p != -1)
            {
                p = data.IndexOf("setup({", p);
                if (p != -1) 
                {
                    if (CountItemsBeforeP(data, p) < 2)//确保词条在前两项
                    {
                        const string startStr = "fbtext: '";
                        const string endStr = "',";

                        int startP = data.IndexOf(startStr, p);
                        if (startP != -1)
                        {
                            int endP = data.IndexOf(endStr, startP);
                            if (endP != -1)
                            {
                                string ans = data.Substring(startP + startStr.Length, endP - (startP + endStr.Length));
                                int existCnt = 0;//正确答案存在个数
                                int index = 0;//正确答案下标
                                for (int i = 0; i < answerArr.Length; i++)
                                {
                                    if (ans.Contains(answerArr[i]))
                                    {
                                        existCnt++;
                                        index = i;
                                    }
                                }

                                if (existCnt == 1) //存在个数，只有一个才能确保是正确选项
                                {
                                    return index;
                                }
                            }
                        }
                    }
                }
            }

            #endregion

            #region 百度百科
            p = data.IndexOf("mu=\"https://baike.baidu.com/item/");
            if (p != -1)
            {
                if (CountItemsBeforeP(data, p) < 3)//确保词条在前三项
                {
                    const string startStr = "百度百科</a>";
                    const string endStr = "baike.baidu.com";

                    int startP = data.IndexOf(startStr, p);
                    int endP = data.IndexOf(endStr, startP);
                    if (startP != -1 && endP != -1)
                    {
                        string ans = data.Substring(startP + startStr.Length, endP - (startP + endStr.Length));
                        int existCnt = 0;//正确答案存在个数
                        int index = 0;//正确答案下标
                        for (int i = 0; i < answerArr.Length; i++)
                        {
                            if (ans.Contains(answerArr[i]))
                            {
                                existCnt++;
                                index = i;
                            }
                        }

                        if (existCnt == 1) //存在个数，只有一个才能确保是正确选项
                        {
                            return index;
                        }
                    }
                }
            }
            #endregion

            return -1;
        }

        /// <summary>
        /// 计算在P位置前面共有多少个搜索结果
        /// </summary>
        /// <param name="data">整个搜索结果页面</param>
        /// <param name="p">搜寻的位置</param>
        /// <returns></returns>
        public static int CountItemsBeforeP(string data, int p)
        {
            int cnt = 0;
            int searchP = data.IndexOf(Properties.Resources.Str_BeforeItem);
            while (searchP != -1 && searchP <= p) 
            {
                cnt++;
                searchP = data.IndexOf(Properties.Resources.Str_BeforeItem, searchP + Properties.Resources.Str_BeforeItem.Length);
            }
            return cnt;
        }
    }
}
