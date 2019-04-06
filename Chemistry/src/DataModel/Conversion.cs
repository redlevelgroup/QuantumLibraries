﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Quantum.Simulation.Core;

using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Quantum.Chemistry;
using System.Numerics;

namespace Microsoft.Quantum.Chemistry.Conversion
{
    /// <summary>
    /// Class containing methods for converting orbital integrals to fermion terms.
    /// </summary>
    public static class OrbitalIntegralToFermionTerm
    {

        /// <summary>
        /// Method for constructing a fermion Hamiltonian from an orbital integral Hamiltonina.
        /// </summary>
        /// <param name="sourceHamiltonian">Input orbital integral Hamiltonian.</param>
        /// <param name="indexConvention">Indexing scheme from spin-orbitals to integers.</param>
        /// <returns></returns>
        public static FermionHamiltonian ToFermionHamiltonian(
            this OrbitalIntegralHamiltonian sourceHamiltonian, 
            SpinOrbital.Config.IndexConvention.Type indexConvention)
        {
            var hamiltonian = new FermionHamiltonian();
            Func<OrbitalIntegral, double, List<(HermitianFermionTerm, double)>> conversion = 
                (orb, coeff) => new OrbitalIntegral(orb.OrbitalIndices, coeff).ToHermitianFermionTerms(indexConvention);

            foreach (var termType in sourceHamiltonian.terms)
            {
                foreach(var term in termType.Value)
                {
                    hamiltonian.AddTerms(conversion(term.Key, term.Value));
                }
            }
            return hamiltonian;
        }

        /// <summary>
        /// Creates all fermion terms generated by all symmetries of an orbital integral.
        /// </summary>
        /// <param name="termType">Orbital integral representing some number of fermion terms.</param>
        public static List<(HermitianFermionTerm, double)> ToHermitianFermionTerms(
            this OrbitalIntegral orbitalIntegral,
            SpinOrbital.Config.IndexConvention.Type indexConvention)
        {
            var termType = orbitalIntegral.GetTermType();
            if (termType == TermType.OrbitalIntegral.OneBody)
            {
                return CreateOneBodySpinOrbitalTerms(orbitalIntegral, indexConvention);
            }
            else if (termType == TermType.OrbitalIntegral.TwoBody)
            {
                return CreateTwoBodySpinOrbitalTerms(orbitalIntegral, indexConvention);
            }
            {
                throw new System.NotImplementedException();
            }
        }
        
        #region Create canonical fermion terms from orbitals
        /// <summary>
        /// Updates an instance of <see cref="FermionHamiltonian"/>
        /// with all spin-orbitals from described by a sequence of two-body orbital integrals.
        /// </summary>
        /// <param name="nOrbitals">Total number of distinct orbitals.</param>
        /// <param name="hpqTerms">Sequence of two-body orbital integrals.</param>
        /// <param name="hamiltonian">Fermion Hamiltonian to be updated.</param>
        private static List<(HermitianFermionTerm, double)> CreateOneBodySpinOrbitalTerms(
            OrbitalIntegral orbitalIntegral, 
            SpinOrbital.Config.IndexConvention.Type indexConvention)
        {
            List<(HermitianFermionTerm, double)> fermionTerms = new List<(HermitianFermionTerm, double)>();
            // One-electron orbital integral symmetries
            // ij = ji
            var pqSpinOrbitals = orbitalIntegral.EnumerateOrbitalSymmetries().EnumerateSpinOrbitals(indexConvention);

            var coefficient = orbitalIntegral.Coefficient;

            foreach (var pq in pqSpinOrbitals)
            {
                var pInt = Convert.ToInt32(pq[0].ToInt());
                var qInt = Convert.ToInt32(pq[1].ToInt());
                var tmp = new HermitianFermionTerm(new[] { pInt, qInt });
                if (pInt == qInt)
                {
                    fermionTerms.Add((new HermitianFermionTerm(new[] { pInt, qInt }), orbitalIntegral.Coefficient));
                }
                else if (pInt < qInt)
                {
                    fermionTerms.Add((new HermitianFermionTerm(new[] { pInt, qInt }), 2.0 * orbitalIntegral.Coefficient));
                }
            }
            return fermionTerms;
        }

        /// <summary>
        /// Updates an instance of <see cref="FermionHamiltonian"/>
        /// with all spin-orbitals from described by a sequence of four-body orbital integrals.
        /// </summary>
        /// <param name="nOrbitals">Total number of distinct orbitals.</param>
        /// <param name="rawPQRSTerms">Sequence of four-body orbital integrals.</param>
        /// <param name="hamiltonian">Fermion Hamiltonian to be updated.</param>
        private static List<(HermitianFermionTerm, double)> CreateTwoBodySpinOrbitalTerms(
            OrbitalIntegral orbitalIntegral,
            SpinOrbital.Config.IndexConvention.Type indexConvention)
        {
            List<(HermitianFermionTerm, double)> fermionTerms = new List<(HermitianFermionTerm, double)>();
            // Two-electron orbital integral symmetries
            // ijkl = lkji = jilk = klij = ikjl = ljki = kilj = jlik.
            var pqrsSpinOrbitals = orbitalIntegral.EnumerateOrbitalSymmetries().EnumerateSpinOrbitals(indexConvention);
            var coefficient = orbitalIntegral.Coefficient;


            // We only need to see one of these.
            // Now iterate over pqrsArray
            foreach (var pqrs in pqrsSpinOrbitals)
            {
                var p = pqrs[0];
                var q = pqrs[1];
                var r = pqrs[2];
                var s = pqrs[3];

                var pInt = Convert.ToInt32(p.ToInt());
                var qInt = Convert.ToInt32(q.ToInt());
                var rInt = Convert.ToInt32(r.ToInt());
                var sInt = Convert.ToInt32(s.ToInt());

                // Only consider terms on the lower diagonal due to Hermitian symmetry.

                // For terms with two different orbital indices, possibilities are
                // PPQQ (QQ = 0), PQPQ, QPPQ (p<q), PQQP, QPQP (p<q), QQPP (PP=0)
                // Hence, if we only count PQQP, and PQPQ, we need to double the coefficient.
                // iU jU jU iU | iU jD jD iD | iD jU jU iD | iD jD jD iD
                if (pInt == sInt && qInt == rInt && pInt < qInt)
                {   // PQQP
                    fermionTerms.Add((new HermitianFermionTerm(new[] { pInt, qInt, rInt, sInt }), 1.0 * coefficient ));
                }
                else if (pInt == rInt && qInt == sInt && pInt < qInt)
                {
                // iU jU iU jU | iD jD iD jD
                // PQPQ
                    fermionTerms.Add((new HermitianFermionTerm(new[] { pInt, qInt, sInt, rInt }), -1.0 * coefficient ));
                }
                else if (qInt == rInt && pInt < sInt && rInt != sInt && pInt != qInt)
                {
                    // PQQR
                    // For any distinct pqr, [i;j;j;k] generates PQQR ~ RQQP ~ QPRQ ~ QRPQ. We only need to record one.
                    if (rInt < sInt)
                    {
                        if (pInt < qInt)
                        {
                            fermionTerms.Add((new HermitianFermionTerm(new[]  { pInt, qInt, sInt, rInt }), -2.0 * coefficient ));
                        }
                        else
                        {
                            fermionTerms.Add((new HermitianFermionTerm(new[]  { qInt, pInt, sInt, rInt }), 2.0 * coefficient ));
                        }

                    }
                    else
                    {
                        if (pInt < qInt)
                        {
                            fermionTerms.Add((new HermitianFermionTerm(new[]  { pInt, qInt, rInt, sInt }), 2.0 * coefficient ));
                        }
                        else
                        {
                            fermionTerms.Add((new HermitianFermionTerm(new[]  { qInt, pInt, rInt, sInt }), -2.0 * coefficient ));
                        }
                    }
                }
                else if (qInt == sInt && pInt < rInt && rInt != sInt && pInt != sInt)
                {
                    // PQRQ
                    // For any distinct pqr, [i;j;k;j] generates {p, q, r, q}, {q, r, q, p}, {q, p, q, r}, {r, q, p, q}. We only need to record one.
                    if (pInt < qInt)
                    {
                        if (rInt > qInt)
                        {
                            fermionTerms.Add((new HermitianFermionTerm(new[]  { pInt, qInt, rInt, sInt }), 2.0 * coefficient ));
                        }
                        else
                        {
                            fermionTerms.Add((new HermitianFermionTerm(new[]  { pInt, qInt, sInt, rInt }), -2.0 * coefficient ));
                        }
                    }
                    else
                    {
                        fermionTerms.Add((new HermitianFermionTerm(new[]  { qInt, pInt, rInt, sInt }), -2.0 * coefficient ));
                    }
                }
                else if (pInt < qInt && pInt < rInt && pInt < sInt && qInt != rInt && qInt != sInt && rInt != sInt)
                {
                    // PQRS
                    // For any distinct pqrs, [i;j;k;l] generates 
                    // {{p, q, r, s}<->{s, r, q, p}<->{q, p, s, r}<->{r, s, p, q}, 
                    // {1,2,3,4}<->{4,3,2,1}<->{2,1,4,3}<->{3,4,1,2}
                    // {p, r, q, s}<->{s, q, r, p}<->{r, p, s, q}<->{q, s, p, r}}
                    // 1324, 4231, 3142, 2413
                    if (rInt < sInt)
                    {
                        fermionTerms.Add((new HermitianFermionTerm(new[]  { pInt, qInt, sInt, rInt }), -2.0 * coefficient ));
                    }
                    else
                    {
                        fermionTerms.Add((new HermitianFermionTerm(new[]  { pInt, qInt, rInt, sInt }), 2.0 * coefficient ));
                    }
                }
            }
            return fermionTerms;
        }

        #endregion

    }
    
}



