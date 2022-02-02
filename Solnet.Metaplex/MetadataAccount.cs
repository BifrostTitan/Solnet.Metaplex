using Solnet.Wallet;
using Solnet.Rpc;
using Solnet.Rpc.Models;
using Solnet.Programs.Utilities;
using Solnet.Rpc.Utilities;

using System.Collections.Generic;
using System;
using System.Text;
using System.Threading.Tasks;


namespace Solnet.Metaplex 
{
    /// <summary> version and type fo metadata account </summary>
    public enum MetadataKey
    {
        /// <summary> Uninitialized </summary>
        Uninitialized = 0,
        /// <summary> Metadata V1 </summary>
        MetadataV1 = 4,
        /// <summary> Edition V1 </summary>
        EditionV1 = 1,
        /// <summary> Master Edition V2 </summary>
        MasterEditionV1 = 2,
        /// <summary> Master Edition V2 </summary>
        MasterEditionV2 = 6,
        /// <summary> Edition marker </summary>
        EditionMarker = 7
    }

    /// <summary> Category </summary>
    public class MetadataCategory 
    {
        /// <summary> Audio </summary>
        public string Audio = "audio";
        /// <summary> Video </summary>
        public string Video = "video";
        /// <summary> Image </summary>
        public string Image = "image";
        /// <summary> Virtual reality, 3D </summary>
        public string VR = "vr";
        /// <summary> html </summary>
        public string HTML = "html";
        /// <summary> Epubs </summary>
        public string Document = "document";
    }

    /// <summary> Metatada file struct </summary>
    public struct MetadataFile
    {
        /// <summary> uri </summary>
        public string uri;
        /// <summary> type </summary>
        public string type;
    }

    /// <summary> Metadata data field </summary>
    public class Data
    {
        /// <summary> name </summary>
        public string name;
        /// <summary> short symbol </summary>
        public string symbol;
        /// <summary> uri of metadata </summary>
        public string uri;
        /// <summary> Seller cut </summary>
        public uint sellerFeeBasisPoints;
        /// <summary> Creators array </summary>
        public IList<Creator> creators;

        /// <summary> Constructor </summary>
        public Data( string name, string symbol, string uri, uint sellerFee, IList<Creator> creators)
        {
            this.name = name;
            this.symbol = symbol;
            this.uri = uri;
            this.sellerFeeBasisPoints = sellerFee;
            this.creators = creators;
        }
    }

    /// <summary> Metadata account class </summary>
    public class MetadataAccount 
    {
        /// <summary> metadata public key </summary>
        public PublicKey metadataKey;
        /// <summary> update authority key </summary>
        public PublicKey updateAuthority;
        /// <summary> mint public key </summary>
        public string mint;
        /// <summary> data struct </summary>
        public Data data;
        /// <summary> is it mutable? </summary>
        public bool isMutable;
        /// <summary> edition nonce </summary>
        public uint editionNonce;

        /// <summary> standard Solana account info </summary>
        public AccountInfo accInfo;
        /// <summary> owner, should be Metadata program</summary>
        public PublicKey owner;

        /// <summary> Constructor </summary>
        /// <param name="accInfo"> Soloana account info </param>
        public MetadataAccount( AccountInfo accInfo )
        {
            try
            {
                this.owner = new PublicKey(accInfo.Owner);
                this.data = ParseData( accInfo.Data);

                var data = Convert.FromBase64String( accInfo.Data[0]);
                this.updateAuthority = new PublicKey(data[1..33]);
                this.mint = new PublicKey(data[33..65]);
            } 
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        /// <summary> Parse data </summary>
        /// <param name="data"> data </param>
        /// <returns> data struct </returns>
        /// <remarks> parses an array of bytes into a data struct </remarks>
        public static Data ParseData( List<string> data)
        {
            try
            {
                byte[] bytes = Convert.FromBase64String(data[0]);
                ReadOnlySpan<byte> binData = new(bytes);

                string name;
                string symbol;
                string uri;

                binData.GetString( MetadataAccountLayout.nameOffset, out name);
                binData.GetString( MetadataAccountLayout.symbolOffset, out symbol);
                binData.GetString( MetadataAccountLayout.uriOffset, out uri);

                uint sellerFee = binData.GetU16( MetadataAccountLayout.feeBasisOffset );

                var numOfCreators = binData.GetU16( MetadataAccountLayout.creatorsOffset );
                var creators = MetadataProgramData.DecodeCreators( binData.GetSpan( 
                    MetadataAccountLayout.creatorsOffset + 4  , 
                    numOfCreators * ( 32 + 1 +1)
                ));

                var res = new Data(
                    name,symbol,uri,sellerFee,creators
                );

                return res;
            }
            catch (Exception ex)
            {
                throw new Exception("could not decode account data from base64", ex);
            }
        }

        /// <summary> Tries to parse a metadata account </summary>
        /// <param name="client"> solana rpcclient </param>
        /// <param name="pk"> public key of a account to parse </param>
        /// <returns> Metadata account </returns>
        /// <remarks> it will try to find a metadata even from a token associated account </remarks>
        async public static Task<MetadataAccount> GetAccount ( IRpcClient client , PublicKey pk )
        {
            var accInfoResponse = await client.GetAccountInfoAsync( pk.Key );

            if ( accInfoResponse.WasSuccessful) 
            {
                var accInfo = accInfoResponse.Result.Value;

                if (accInfo.Owner.Contains("meta"))
                {
                    return new MetadataAccount(accInfo);
                } 
                else //if(accInfo.Owner.Contains("Token")) 
                {
                    var readdata = Convert.FromBase64String(accInfo.Data[0]);

                    PublicKey mintAccount;

                    if( readdata.Length == 165 )
                    {
                        mintAccount = new PublicKey(readdata[..32]);
                    } else //if( readdata.Length == 82)
                    {
                        mintAccount = pk;
                    }

                    byte[] metadataAddress = new byte[32];
                    int nonce;
                    AddressExtensions.TryFindProgramAddress(
                        new List<byte[]>() {
                            Encoding.UTF8.GetBytes("metadata"),
                            MetadataProgram.ProgramIdKey,
                            mintAccount
                        },
                        MetadataProgram.ProgramIdKey,
                        out metadataAddress,
                        out nonce
                    );

                    return await GetAccount(client, new PublicKey(metadataAddress));
                }
            }
            else
            {
                return null;
            }
        } 

        // async public static MetadataAccount GetAccount ( string query )
        // {

        // }
    }
}