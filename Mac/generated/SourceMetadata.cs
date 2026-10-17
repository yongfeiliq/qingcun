using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

partial class Host {
    static string SourceLabel(string value){return Cut(Regex.Replace((value??"").Replace("\0","").Trim().TrimStart('#'),@"\s+"," "),240);}
    static string[] SourceLabels(IEnumerable<string> values){return values.Select(SourceLabel).Where(x=>x!="").Distinct(StringComparer.OrdinalIgnoreCase).Take(1000).ToArray();}
    static List<Dictionary<string,object>> DescriptionFacts(string description){
        var facts=new List<Dictionary<string,object>>();
        var names=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase){{"type","制作方式"},{"software","软件"},{"product","产品"},{"client","客户"},{"3d work","三维制作"},{"制作方式","制作方式"},{"软件","软件"},{"产品","产品"},{"客户","客户"}};
        foreach(string line in DescriptionText(description).Split('\n')){
            var m=Regex.Match(line.Trim(),@"^([^:：]{1,35})\s*[:：]\s*(.+)$");string name;
            if(!m.Success||!names.TryGetValue(m.Groups[1].Value.Trim(),out name))continue;
            string value=Regex.Replace(m.Groups[2].Value,@"[\u200b-\u200f\ufeff]","").Trim();
            if(value!="")facts.Add(Obj("label",name,"value",Cut(value,600),"source","description","evidence",Cut(line.Trim(),800)));
        }return facts.Take(30).ToList();
    }
    static void MergePlatformTags(Dictionary<string,object> a,Dictionary<string,object> meta,Dictionary<string,object> portable){
        a["originalTags"]=SourceLabels(Strings(a,"originalTags").Concat(Strings(portable,"originalTags")).Concat(Strings(meta,"tags")));
        a["sourceCategories"]=SourceLabels(Strings(a,"sourceCategories").Concat(Strings(portable,"sourceCategories")).Concat(Strings(meta,"categories")));
        var tags=Items(a,"tags");var dismissed=new HashSet<string>(Strings(a,"dismissedTags"));
        Action<string,string,string> add=(dimension,name,evidence)=>{var t=Tag(dimension,name,"metadata",evidence);if(S(t,"name")!=""&&!dismissed.Contains(TagKey(t))&&!tags.Any(old=>TagKey(old)==TagKey(t)))tags.Add(t);};
        foreach(string name in Strings(a,"originalTags"))add("平台关键词",name,"平台公开的原始标签，保留原名");
        foreach(string name in Strings(a,"sourceCategories"))add("平台分类",name,"平台公开的原始分类，保留原名");
        var facts=DescriptionFacts(S(meta,"description"));
        if(facts.Count>0)a["sourceFacts"]=facts;else if(!a.ContainsKey("sourceFacts"))a["sourceFacts"]=Items(portable,"sourceFacts");
        foreach(var fact in Items(a,"sourceFacts")){
            string label=S(fact,"label"),value=S(fact,"value");
            if(label=="制作方式")add(label,value,S(fact,"evidence"));
            if(label=="软件")foreach(string software in Regex.Split(value,@",\s*|\s+&\s+|\s+with\s+",RegexOptions.IgnoreCase))add("软件",software,S(fact,"evidence"));
        }
        a["tags"]=tags;
    }
    static void LinkNamedPublisherCredits(Dictionary<string,object> a){
        var publisher=D(a,"publisher");
        if(S(publisher,"name")==""||S(publisher,"url")=="")return;
        foreach(var c in Items(a,"credits"))
            if(S(c,"source")=="metadata"&&S(c,"status")!="confirmed"&&S(c,"url")==""&&S(c,"name").Trim().Equals(S(publisher,"name").Trim(),StringComparison.OrdinalIgnoreCase)){
                c["account"]=S(publisher,"account");c["url"]=S(publisher,"url");c["platform"]=S(publisher,"platform");c["accountStatus"]="署名与发布者名称一致";c["evidence"]=S(c,"evidence")+"；账号链接来自同名发布者";
            }
    }
}
