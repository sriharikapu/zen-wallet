﻿using System;
using System.Web.Configuration;
using Zen.RPC.Common;
using Zen.RPC;
using System.Linq;
using Consensus;
using Sodium;
using System.Text;
using System.Collections.Generic;
using Wallet.core.Data;

namespace Datafeed.Web.App_Code
{
	public class ContractManager
	{
		static string NodeRPCAddress = WebConfigurationManager.AppSettings["node"];
		static string walletPrivateKey = WebConfigurationManager.AppSettings["walletPrivateKey"];

        public string Message { get; private set; }
		public bool IsSetup { get; private set; }
		public Address ContractAddress { get; private set; }
		public byte[] PrivateKey { get; private set; }

		public ContractManager()
        {
            if (!EnsureFunds())
            {
                Message = "No funds";
                IsSetup = false;
            }

            var keypair = EnsureKeyPair();
            PrivateKey = keypair.PrivateKey;
            var contractCode = EnsureContractCode(keypair.PublicKey);

			ContractAddress = new Address(Merkle.innerHash(Encoding.ASCII.GetBytes(contractCode)), AddressType.Contract);

			if (!EnsureContractActive(contractCode))
			{
				Message = "Could not activate Contract";
				IsSetup = false;
			}

			if (!EnsureInitialOutpoint())
			{
				Message = "Could not create initial outpoint";
				IsSetup = false;
			}

            Message = "";
			IsSetup = true;
        }

		bool EnsureFunds()
		{
			if (HasFunds())
			{
				return true;
			}

			if (!Acquire())
			{
				return false;
			}

			return HasFunds();
		}

		bool HasFunds()
		{
			var getBalanceResultPayload = Client.Send<GetBalanceResultPayload>(NodeRPCAddress, new GetBalancePayload() { Asset = Consensus.Tests.zhash }).Result;

			if (!getBalanceResultPayload.Success)
			{
				return false;
			}

			return getBalanceResultPayload.Balance > 0;
		}

	    bool Acquire()
		{
			return Client.Send<ResultPayload>(NodeRPCAddress, new AcquirePayload() { PrivateKey = walletPrivateKey }).Result.Success;
		}

        KeyPair EnsureKeyPair()
        {
            if (!System.IO.File.Exists(System.IO.Path.Combine("db", "oracle-key.txt")))
			{
				var keyPair = PublicKeyAuth.GenerateKeyPair();
                System.IO.File.WriteAllText(System.IO.Path.Combine("db", "oracle-key.txt"), Convert.ToBase64String(keyPair.PublicKey) + " " + Convert.ToBase64String(keyPair.PrivateKey));
                return keyPair;
            }

            var data = System.IO.File.ReadAllText(System.IO.Path.Combine("db", "oracle-key.txt"));
			var parts = data.Split(null);
            return new KeyPair(Convert.FromBase64String(parts[0]), Convert.FromBase64String(parts[1]));
        }

        string EnsureContractCode(byte[] publicKey)
        {
            if (System.IO.File.Exists(System.IO.Path.Combine("db", "oracle-contract.txt")))
            {
                return System.IO.File.ReadAllText(System.IO.Path.Combine("db", "oracle-contract.txt"));
            }

            var @params = new ContractExamples.QuotedContracts.OracleParameters(publicKey);
            var contract = ContractExamples.QuotedContracts.oracleFactory(@params);
            var contractCode = ContractExamples.Execution.quotedToString(contract);
            System.IO.File.WriteAllText(System.IO.Path.Combine("db", "oracle-contract.txt"), contractCode);
            return contractCode;
        }

        bool EnsureContractActive(string contractCode)
        {
            if (IsContractActive())
			{
				return true;
			}

            return ActivateContract(contractCode);
        }

        bool IsContractActive()
        {
			var acsResult = Client.Send<GetACSResultPayload>(NodeRPCAddress, new GetACSPayload()).Result;

			if (!acsResult.Success)
			{
				return false;
			}

            return acsResult.Contracts.Count(t => t.Hash.SequenceEqual(ContractAddress.Bytes)) == 1;
		}

        bool ActivateContract(string contractCode)
        {
            //TODO: extend contract if remaining blocks is less than ~10
			var activateContractResult = Client.Send<ResultPayload>(NodeRPCAddress, new ActivateContractPayload() { Code = contractCode, Blocks = 1000 }).Result;

            return activateContractResult.Success;
		}

		bool EnsureInitialOutpoint()
		{
			if (HasInitialOutpoint())
			{
				return true;
			}

            return SetInitialOutpoint();
		}

		bool HasInitialOutpoint()
		{
			var getOutpointsResult =
				Client.Send<GetContractPointedOutputsResultPayload>(NodeRPCAddress, new GetContractPointedOutputsPayload() { ContractHash = ContractAddress.Bytes }).Result;

			if (!getOutpointsResult.Success)
			{
				return false;
			}

            return getOutpointsResult.PointedOutputs.Count > 0;
		}

		bool SetInitialOutpoint()
        {
			var sendContractResult = Client.Send<ResultPayload>(NodeRPCAddress, new SpendPayload() { Address = ContractAddress.ToString(), Amount = 1 }).Result;

            return sendContractResult.Success;
		}
	}
}