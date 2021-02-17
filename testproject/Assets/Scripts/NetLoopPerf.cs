using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

public class NetLoopPerf : MonoBehaviour
{
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            StartCoroutine(Benchmark(128));
        }

        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            StartCoroutine(Benchmark(1024));
        }

        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            StartCoroutine(Benchmark(8 * 1024));
        }
    }

    private IEnumerator Benchmark(int count)
    {
        print($"{nameof(NetLoopPerf)}.{nameof(Benchmark)}(count: {count})");

        var sw = new Stopwatch();

        var gobjs = new List<GameObject>();
        var comps = new List<NetLoopComp>();
        for (int i = 0; i < count; i++)
        {
            var gobj = new GameObject($"{nameof(NetLoopPerf)}.{nameof(Benchmark)} #{i}");
            var comp = gobj.AddComponent<NetLoopComp>();
            gobjs.Add(gobj);
            comps.Add(comp);
        }

        yield return new WaitForEndOfFrame();

        sw.Restart();
        for (int i = 0; i < count; i++)
        {
            comps[i].RegisterUpdates(i);
        }

        sw.Stop();
        print($"{nameof(NetLoopPerf)}.{nameof(Benchmark)} -> register: {sw.ElapsedMilliseconds}ms");

        yield return new WaitForEndOfFrame();

        sw.Restart();
        for (int i = 0; i < 8; i++)
        {
            yield return new WaitForEndOfFrame();
        }

        sw.Stop();
        print($"{nameof(NetLoopPerf)}.{nameof(Benchmark)} -> 8-frames: {sw.ElapsedMilliseconds}ms");

        yield return new WaitForEndOfFrame();

        sw.Restart();
        for (int i = 0; i < count; i++)
        {
            comps[i].UnregisterUpdates();
        }

        sw.Stop();
        print($"{nameof(NetLoopPerf)}.{nameof(Benchmark)} -> unregister: {sw.ElapsedMilliseconds}ms");

        yield return new WaitForEndOfFrame();

        for (int i = 0; i < count; i++)
        {
            DestroyImmediate(gobjs[i]);
        }

        gobjs.Clear();
        comps.Clear();
    }
}