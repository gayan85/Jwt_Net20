﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


namespace TestCoreJWT
{


    public class User
    {
        public int Id = 123;
        public string Name = "Test";

        public string Language = "de-CH";

        public string Bla = "Test\r\n123\u0005äöüÄÖÜñõ";

        [CoreJWT.PetaJson.JsonExclude()]
        private string m_Message = "C'est un \"Teste\" éâÂçäöüÄÖÜ$£€";
        public string Message
        {
            get { return m_Message; }
            set { this.m_Message = value; }
        }


        public string AnotherMessage
        {
            get { return m_Message; }
        }

    } // User 


    public class TestJWT
    {
        public static void Test()
        {

            string pubKey = @"-----BEGIN PUBLIC KEY-----
MIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQKBgQCYk9VtkOHIRNCquRqCl9bbFsGw
HdJJPIoZENwcHYBgnqVoEa5SJs8ddkNNna6M+Gln2n4S/G7Mu+Cz0cQg06Ru8919
hYWGWdyVumAGgJwMEKAzUj9651Y6AAOcAM0qX/f0DrLlUAZFy+64L8kVjuCyFdti
5d3yaGnFM+Xw/4fcLwIDAQAB
-----END PUBLIC KEY-----
";

            string privKey = @"-----BEGIN RSA PRIVATE KEY-----
MIICXQIBAAKBgQCYk9VtkOHIRNCquRqCl9bbFsGwHdJJPIoZENwcHYBgnqVoEa5S
Js8ddkNNna6M+Gln2n4S/G7Mu+Cz0cQg06Ru8919hYWGWdyVumAGgJwMEKAzUj96
51Y6AAOcAM0qX/f0DrLlUAZFy+64L8kVjuCyFdti5d3yaGnFM+Xw/4fcLwIDAQAB
AoGAGyDZ51/FzUpzAY/k50hhEtZSfOJog84ITdmiETurmkJK7ZyLLp8o3zeqUtAQ
+46liyodlXmdp7hWBRLseNu4lh1gQGYj4/fH2BT75/zFngaTdz7pANKq6Y5IOHg0
C1UatzmuSmDGk/l7g1gQyWo8dcwjrzvsGWBAFZ4QHy2OsE0CQQDNStOX0USyfgrZ
AkKOfs3paaxVB/SZTBaorcqo8nBX1Fx/rdpBTIezHuZQchF/BGpHLS7/yyve+jg/
dspR7XZdAkEAvkO10QFsDR1GJwVcUpG1LguznKqS7v6FscnpBFvfsf7UaqNHCGvY
Feau1EwekVRl77ZKUPhDQt7XFniBO40b+wJBALZnQ7Xi1H0bjJvgbC6b8Gzx3ZL3
rJcAiil5sVWHg9Yl88HmQMRAMVovnEfh8jW/QIbZWKciaGqIPK326DD/ImkCQQCC
k1OHQfOWuH15sCshG5B9Lliw7ztxu8mjL0+0xxypOpsrKC1KsUCWHz/iwO7FjGd8
8Nzl3svCa86vRDpk1T3bAkBWjvKigxbkpYPbayKwjeWTiS3YIg63N2WUaetFBAD2
Yrv+Utm12zi99pZNA5WCqO/UhN9poJdWaYqYYImYhH8N
-----END RSA PRIVATE KEY-----
";


            CoreJWT.JwtKey key = new CoreJWT.JwtKey { PemPrivateKey = privKey };
            CoreJWT.JwtKey arbitraryKey = new CoreJWT.JwtKey { PemPrivateKey = CoreJWT.Crypto.GenerateRandomRsaPrivateKey(1024) };

            string token = CoreJWT.JsonWebToken.Encode(new User(), key, CoreJWT.JwtHashAlgorithm.RS256);

            System.Console.WriteLine(token);
            User thisUser = CoreJWT.JsonWebToken.DecodeToObject<User>(token, key, true);
            User wrongUser = CoreJWT.JsonWebToken.DecodeToObject<User>(token, arbitraryKey, true);


            System.Console.WriteLine(thisUser);
            System.Console.WriteLine(wrongUser);


            System.Console.WriteLine(System.Environment.NewLine);
            System.Console.WriteLine(" --- Press any key to continue --- ");
            System.Console.ReadKey();
        }


    }


}
