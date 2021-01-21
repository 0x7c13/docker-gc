namespace DockerGC.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using Xunit;
    using Moq;

    public class MatchlistTest
    {
        [Fact]
        public void ConstructorTest_ShouldReturnFalseIfWhitelistIsEmptyOrInputIsEmpty()
        {   
            var whitelist = new Matchlist();

            Assert.False(whitelist.Match("haha"));
            Assert.False(whitelist.Match(""));
            Assert.False(whitelist.Match(null));

            whitelist = new Matchlist("");

            Assert.False(whitelist.Match("haha"));
            Assert.False(whitelist.Match(""));
            Assert.False(whitelist.Match(null));

            whitelist = new Matchlist(null);

            Assert.False(whitelist.Match("haha"));
            Assert.False(whitelist.Match(""));
            Assert.False(whitelist.Match(null));
        }

        [Fact]
        public void ConstructorTest_ShouldParseInputAndIgnoreEmpty()
        {
            var whitelist = new Matchlist("  imageA,imageB , *imageC, imageD* ,, ,imageE,  ").ToList();

            Assert.True(whitelist.Count == 5);
            Assert.True(whitelist[0] == "imageA");
            Assert.True(whitelist[1] == "imageB");
            Assert.True(whitelist[2] == "*imageC");
            Assert.True(whitelist[3] == "imageD*");
            Assert.True(whitelist[4] == "imageE");
        }

        [Fact]
        public void MatchTest_ShouldMatchExact()
        {
            var whitelist = new Matchlist("imageA,imageB , *imageC, imageD* ,, ,imageE");
            
            Assert.True(whitelist.Match("IMAGEA"));
            Assert.True(whitelist.Match("imageA", false));
            Assert.True(whitelist.Match("*imageC"));
            Assert.True(whitelist.Match("imageD*"));
        }

        [Fact]
        public void MatchTest_ShouldMatchPrefix()
        {
            var whitelist = new Matchlist("*imageC");
            
            Assert.True(whitelist.Match("helloimageC"));
            Assert.True(whitelist.Match("imageC"));
            Assert.False(whitelist.Match("imageChello"));
            Assert.False(whitelist.Match("helloIMAGEC", false));
            Assert.False(whitelist.Match("IMAGEC", false));
        }

        [Fact]
        public void MatchTest_ShouldMatchSuffix()
        {
            var whitelist = new Matchlist("imageD*");
            
            Assert.True(whitelist.Match("imageDhello"));
            Assert.True(whitelist.Match("imageD"));
            Assert.False(whitelist.Match("helloimageD"));
            Assert.False(whitelist.Match("IMAGEDhello", false));
            Assert.False(whitelist.Match("IMAGED", false));
        }

        [Fact]
        public void MatchTest_ShouldMatchSubstring()
        {
            var whitelist = new Matchlist("*image*");
            
            Assert.True(whitelist.Match("1image1"));
            Assert.True(whitelist.Match("image"));
            Assert.True(whitelist.Match("imagehello"));
            Assert.True(whitelist.Match("helloimage"));
            Assert.False(whitelist.Match("mag", false));
            Assert.False(whitelist.Match("helloIMAGE", false));
            Assert.False(whitelist.Match("IMAGE", false));
        }

        [Fact]
        public void MatchAnyTest()
        {
            var whitelist = new Matchlist("imageA,imageB , *imageC, imageD* ,, ,imageE");
            
            Assert.False(whitelist.MatchAny(null));

            var inputs = new List<string>{};
            Assert.False(whitelist.MatchAny(inputs));

            inputs = new List<string>{"haha"};
            Assert.False(whitelist.MatchAny(inputs));

            inputs = new List<string>{"imageA"};
            Assert.True(whitelist.MatchAny(inputs));

            inputs = new List<string>{"imageA", "imageB"};
            Assert.True(whitelist.MatchAny(inputs));

            inputs = new List<string>{"image", "imageA"};
            Assert.True(whitelist.MatchAny(inputs));

            inputs = new List<string>{"lol", "haha", "helloimagec"};
            Assert.True(whitelist.MatchAny(inputs));
        
            inputs = new List<string>{"helloimagec", "imageDhello"};
            Assert.True(whitelist.MatchAny(inputs));
        }

    }
}
