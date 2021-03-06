﻿//module Consensus.Validate

//open Consensus.Types
//open Consensus.Serialization

//[<Measure>] type zen
//[<Measure>] type kalapa

//let zenAssetHash:Hash = Array.zeroCreate ContractHashBytes

//let kalapaPerZen: decimal<kalapa zen^-1> = 100000000M<kalapa/zen>
//let convertKalapaToZen (n:decimal<kalapa>) = n / kalapaPerZen
//let convertZenToKalapa (n:decimal<zen>) = System.Decimal.Floor(decimal(n * kalapaPerZen)) * 1M<kalapa>

//let version o = match o.lock with
//                       | PKLock _ -> 0ul
//                       | ContractLock _ -> 0ul
//                       | FeeLock l -> l.version
//                       | HighVLock l -> l.version
//                       | CoinbaseLock l -> l.version


//let (|SigHashAll | SigHashNone | SigHashSingle | SigHashAnyoneCanPayAll| SigHashAnyoneCanPayNone | SigHashAnyoneCanPaySingle|) sigrules =
//    let acp, outputrule = sigrules &&& 0x80uy, sigrules &&& 0x3uy in
//        if acp<>0uy then 
//            if outputrule = 2uy then SigHashAnyoneCanPayNone
//            elif outputrule = 1uy then SigHashAnyoneCanPaySingle
//            else SigHashAnyoneCanPayAll
//        else
//            if outputrule = 2uy then SigHashNone
//            elif outputrule = 1uy then SigHashSingle
//            else SigHashAll
//let initialReward = 1000M<zen>
//let halving = 1000000ul

//let reward blk =
//    let period = (int)(blk.number / halving)
//    initialReward / (decimal)(pown 2 period)


//let addToMap (map: Map<Hash, uint64>) (asset:Hash) (amount:uint64) = match map.TryFind asset with
//                                                                      | None -> map.Add (asset, amount)
//                                                                      | Some v -> map.Add (asset, v + amount)

//let addOutputToMap (map: Map<Hash,uint64>) (output:Output) = addToMap map output.spend.asset output.spend.amount

//let validWitness transaction, index = match transaction.inputs.[index] with
//                                 | PKLock {pkHash = h} -> true
//                                 | ContractLock _ -> true
//                                 | FeeLock _ -> true
//                                 | HighVLock _ -> true
//                                 | CoinbaseLock _ -> true

//let validTx (tx:Transaction) = 
//    // preliminaries
//    if List.length tx.witnesses <> List.length tx.inputs then Invalid else
//    let inputvals = List.fold addOutputToMap Map.empty tx.inputs
//    let outputvals = List.fold addOutputToMap Map.empty tx.outputs
//    if inputvals <> outputvals then Invalid else
//    if List.exists2 (not << validWitness) inputs witnesses then Invalid else //TODO needs to validate on digests of tx
//    Valid tx.contract

//let blockFees (blk:Block) =
//    blk.transactions
//    |> List.tail // remove coinbase tx
//    |> List.map (fun tx -> tx.outputs)
//    |> List.concat // list of all outputs in block
//    |> List.filter (fun out -> match out.lock with | ContractLock _ -> true | _ -> false)
//    |> List.fold addOutputToMap Map.empty

//let validCoinbase (blk:Block) =
//    if blk.transactions.Length = 0 then Invalid else
//    let tx = blk.transactions.Head
//    if tx.contract <> None then Invalid else
//    // No fee outputs!
//    if List.exists (fun out -> match out.lock with FeeLock _ -> true | _ -> false) tx.outputs then Invalid else
//    if tx.inputs.Length = 0 then Invalid else
//    // All inputs must have CoinbaseLock. Version not currently relevant to coinbase-specific validation.
//    if List.exists (fun inp -> match inp.lock with CoinbaseLock _ -> false | _ -> true) tx.inputs then Invalid else
//    // At most one input per asset class
//    let inCount = List.fold (fun map inp -> addToMap map inp.spend.asset 1UL) Map.empty tx.inputs
//    if Map.exists (fun k v -> v > 1UL) inCount then Invalid else
//    // Calculate the maximum claimable in the coinbase transaction
//    let fees = blockFees blk
//    let blockReward = blk |> reward |> convertZenToKalapa
//    let maxClaim = addToMap fees zenAssetHash (uint64 (decimal blockReward))
//    let claim = List.fold addOutputToMap Map.empty tx.inputs
//    let exceedsMax k v =
//        v <> 0UL &&
//        Map.tryFind k maxClaim
//        |> fun res -> res.IsNone || (res.Value < v)
//    if (Map.exists exceedsMax claim) then Invalid else
//    validTx tx
