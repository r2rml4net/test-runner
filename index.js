#!/usr/bin/env node

const { spawn } = require('child_process');
const clownface = require('clownface-io')
const namespace = require('@rdfjs/namespace')
const { rdf } = require('@tpluscode/rdf-ns-builders')

const ns = {
    doap: namespace('http://usefulinc.com/ns/doap#'),
}

async function main() {
    const { dataset } = await clownface().namedNode(`file:${__dirname}/test harness/ts.ttl`).fetch()

    clownface({ dataset }).has(rdf.type, ns.doap.Project).forEach(project => {
        const suffix = project.out(ns.doap.name).value

        spawn('./run.sh', [
            '-s', suffix
        ], { stdio: 'inherit' })
    })
}

main()
