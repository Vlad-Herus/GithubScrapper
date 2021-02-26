using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace GithubParser
{
    class Program
    {
        #region github classes

        private class PR
        {
            public int number { get; set; }
            public DateTime? merged_at { get; set; }
        }

        private class User
        {
            public string login { get; set; }
        }

        private class Review
        {
            public int id { get; set; }
            public User user { get; set; }
            public string state { get; set; }
        }

        private class ReviewComment
        {
            public int id { get; set; }
            public User user { get; set; }
        }

        private class File
        {
            public int additions { get; set; }
            public int deletions { get; set; }
            public int changes { get; set; }
        }

        #endregion

        private class ParsedPr
        {
            public List<UserPrStats> stats { get; set; }
            public int numberOfCodeLines { get; set; }

        }

        private class UserPrStats
        {
            public string userName { get; set; }
            public int numberOfComments { get; set; }
        }

        private class TotalUserStats
        {
            public string username { get; set; }
            public int totalComments { get; set; }
            public int totalPrs { get; set; }
            public int totalLines { get; set; }
        }

        static async Task Main(string[] args)
        {
            //var allPrs = System.IO.File.ReadAllLines(@"D:\github\prs_2.txt")
            //    .Where(pr => !string.IsNullOrEmpty(pr))
            //    .Select(pr => int.Parse(pr));

            //int batchNumber = 0;
            //int batchSize = 100;

            //List<ParsedPr> res = new List<ParsedPr>();

            //List<TotalUserStats> totalStats = new List<TotalUserStats>();

            //try
            //{

            //    while (true)
            //    {
            //        var batch = allPrs.Skip(batchNumber * batchSize).Take(batchSize);

            //        if (!batch.Any())
            //            break;

            //        var tasks = batch.Select(pr => ParsePr(pr)).ToArray();

            //        var results = await Task.WhenAll(tasks);
            //        res.AddRange(results);
            //        batchNumber++;

            //        if (batchNumber == 7)
            //            throw new Exception();
            //    }
            //}
            //catch
            //{
            //    try
            //    {
            //        var remaining = allPrs.Skip(batchNumber * batchSize);
            //        System.IO.File.WriteAllLines(@"D:\github\prs_3.txt", remaining.Select(n => n.ToString()));


            //        var names = res.SelectMany(pr => pr.stats.Select(stat => stat.userName)).Distinct();



            //        foreach (var prRes in res)
            //        {
            //            foreach (var stat in prRes.stats)
            //            {
            //                if (!totalStats.Any(ts => ts.username == stat.userName))
            //                    totalStats.Add(new TotalUserStats { username = stat.userName });

            //                var totalStat = totalStats.Single(ts => ts.username == stat.userName);
            //                totalStat.totalComments += stat.numberOfComments;
            //                totalStat.totalPrs += 1;
            //                totalStat.totalLines += prRes.numberOfCodeLines;
            //            }
            //        }

            //        var lines = new List<string>();
            //        foreach (var totalStat in totalStats)
            //        {
            //            lines.Add($"{totalStat.username}, {totalStat.totalPrs}, {totalStat.totalLines}, {totalStat.totalComments}");
            //        }
            //        System.IO.File.WriteAllLines(@"D:\github\stats_3.txt", lines);
            //    }
            //    catch (Exception ex)
            //    { 

            //    }

            //}
            ////await ParsePr(1);

            //Console.WriteLine("Hello World!");

            CombineResults(new string[] { @"D:\github\stats_1.txt", @"D:\github\stats_2.txt" });
        }

        private static HttpClient GetHttpClient()
        {
            var userName = "%account%";
            var passwd = "%token%";

            var authToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{userName}:{passwd}"));


            HttpClient httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authToken);
            httpClient.DefaultRequestHeaders.Add("User-Agent", "manual");

            return httpClient;
        }

        private static async Task DumpPrs()
        {

            var httpClient = GetHttpClient();
            httpClient.BaseAddress = new Uri("https://api.github.com/repos/%company%/%repo%/pulls");

            int page = 1;
            List<int> numbers = new List<int>();
            bool endLoop = false;
            while (true)
            {

                var prsResponse = await httpClient.GetAsync($"?state=closed&page={page}");
                var prsString = await prsResponse.Content.ReadAsStringAsync();
                page++;

                var prObjects = JsonConvert.DeserializeObject<IEnumerable<PR>>(prsString);

                foreach (var pr in prObjects)
                {
                    if (pr.merged_at != null)
                    {
                        if (pr.merged_at < (DateTime.Now - TimeSpan.FromDays(365)))
                        {
                            endLoop = true;
                            break;
                        }
                        numbers.Add(pr.number);
                    }
                }

                if (endLoop)
                    break;
            }

            System.IO.File.WriteAllLines(@"D:\github\prs.txt", numbers.Select(n => n.ToString()));
        }

        private static async Task<ParsedPr> ParsePr(int number)
        {
            string response = "";
            try
            {
                var httpClient = GetHttpClient();
                httpClient.BaseAddress = new Uri($"https://api.github.com/repos/%company%/%repo%/pulls/");

                var reviewsResponse = await httpClient.GetAsync($"{number}/reviews?per_page=100");
                var reviewsString = await reviewsResponse.Content.ReadAsStringAsync();
                response = reviewsString;
                var reviewsObjects = JsonConvert.DeserializeObject<IEnumerable<Review>>(reviewsString);

                List<string> usersApproved = new List<string>();

                foreach (var review in reviewsObjects)
                {
                    if (review.state == "APPROVED")
                        usersApproved.Add(review.user.login);
                }

                List<UserPrStats> stats = new List<UserPrStats>();
                foreach (var user in usersApproved.Distinct())
                {
                    stats.Add(new UserPrStats { userName = user, numberOfComments = 0 });
                }

                List<ReviewComment> comments = new List<ReviewComment>();

                foreach (var review in reviewsObjects)
                {
                    var commentResponse = await httpClient.GetAsync($"{number}/reviews/{review.id}/comments?per_page=100");
                    var commentString = await commentResponse.Content.ReadAsStringAsync();
                    response = commentString;
                    var commentObjects = JsonConvert.DeserializeObject<IEnumerable<ReviewComment>>(commentString);

                    foreach (var comment in commentObjects)
                    {
                        if (!comments.Any(c => c.id == comment.id))
                            comments.Add(comment);
                    }

                }

                foreach (var comment in comments)
                {
                    if (usersApproved.Contains(comment.user.login))
                    {
                        var stat = stats.Single(s => s.userName == comment.user.login);
                        stat.numberOfComments += 1;
                    }
                }



                var filesResponse = await httpClient.GetAsync($"{number}/files?per_page=100");
                var filesString = await filesResponse.Content.ReadAsStringAsync();
                response = filesString;
                var filesObjects = JsonConvert.DeserializeObject<IEnumerable<File>>(filesString);

                int total = 0;

                foreach (var file in filesObjects)
                    total += file.changes;

                return new ParsedPr { stats = stats, numberOfCodeLines = total };
            }
            catch (Exception ex)
            {
                Console.WriteLine(number);

                int n = 1 + 1;

                if (n == 22)
                {
                    throw;
                }
            }
            return null;

        }

        private static void CombineResults(IEnumerable<string> filePathes)
        {
            List<TotalUserStats> totalStats = new List<TotalUserStats>();

            foreach (var file in filePathes)
            {
                var lines = System.IO.File.ReadAllLines(file).ToArray();
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    var tokens = line.Split(',');
                    TotalUserStats stat = new TotalUserStats
                    { 
                        username = tokens[0],
                        totalPrs = int.Parse(tokens[1]),
                        totalLines = int.Parse(tokens[2]),
                        totalComments= int.Parse(tokens[3]),
                    };

                    var existing = totalStats.SingleOrDefault(s => s.username == stat.username);

                    if (existing != null)
                    {
                        existing.totalPrs += stat.totalPrs;
                        existing.totalLines += stat.totalLines;
                        existing.totalComments += stat.totalComments;
                    }
                    else
                        totalStats.Add(stat);
                }

                
            }

            var lines2 = new List<string>();
            foreach (var totalStat in totalStats)
            {
                lines2.Add($"{totalStat.username}, {totalStat.totalPrs}, {totalStat.totalLines}, {totalStat.totalComments}");
            }
            var l = lines2.ToArray();
            System.IO.File.WriteAllLines(@"D:\github\stats_ALL.txt", lines2);
        }
    }
}
