using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CEParser;

namespace Ironmelt
{
    struct Game
    {
        public string Name;
        public string Ext;
        public string[] BinaryTokens;
        public string LocalFolderPath;
        public string CloudFolderPath;
        public Encoding Encoding;

        public Game(string name, string ext, string binarytokensfile, string localpath, string cloudpath, Encoding encoding)
        {
            this.Name = name;
            this.Ext = ext;
            this.LocalFolderPath = localpath;
            this.CloudFolderPath = cloudpath;
            this.BinaryTokens = CEParser.Ironmelt.ReadTokensFile(binarytokensfile);            
            this.Encoding = encoding;
        }
    }
}
