module Zen.Merkle

open Zen.Cost
module  A = Zen.Array
module  M = FStar.Mul
module  C = Zen.Crypto


val rootFromAuditPath: #n:nat
  -> C.hash
  -> nat
  -> A.t C.hash n
  -> cost (C.hash) n
