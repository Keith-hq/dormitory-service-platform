# -*- coding: utf-8 -*-
"""C8 安全社区黑盒测试（王浩宇）—— 访客二维码 + 房间投票一人一票。

前置：后端 http://localhost:5000 已启动；种子数据 database/seed/c8_blackbox_seed.sql 已灌入。
用法：python C8_blackbox_test.py
"""
import json
import sys
import urllib.request
import urllib.error
import datetime

sys.stdout.reconfigure(encoding='utf-8', errors='replace')
sys.stderr.reconfigure(encoding='utf-8', errors='replace')

BASE = "http://localhost:5000"
results = []


def call(method, path, token=None, body=None):
    url = BASE + path
    data = None
    headers = {}
    if body is not None:
        data = json.dumps(body, ensure_ascii=True).encode("utf-8")
        headers["Content-Type"] = "application/json"
    if token:
        headers["Authorization"] = "Bearer " + token
    req = urllib.request.Request(url, data=data, headers=headers, method=method)
    try:
        with urllib.request.urlopen(req) as resp:
            code = resp.status
            raw = resp.read().decode("utf-8")
    except urllib.error.HTTPError as e:
        code = e.code
        raw = e.read().decode("utf-8")
    try:
        parsed = json.loads(raw)
    except Exception:
        parsed = raw
    return code, parsed


def check(label, method, path, token, body, expect_code=None, expect_field=None, expect_val=None):
    code, parsed = call(method, path, token, body)
    ok = True
    if expect_code is not None:
        ok = ok and code == expect_code
    if expect_field is not None and isinstance(parsed, dict):
        actual = parsed.get("data", parsed)
        if isinstance(actual, dict):
            ok = ok and actual.get(expect_field) == expect_val
        else:
            ok = ok and (expect_val is None)
    status = "PASS" if ok else "FAIL"
    results.append((status, label))
    print(f"[{status}] {label} -> HTTP {code}")
    print(f"       {json.dumps(parsed, ensure_ascii=False)[:300]}")
    return code, parsed


def login(name, pwd="Test1234"):
    code, parsed = call("POST", "/api/auth/login", body={"loginName": name, "password": pwd})
    if code == 200 and isinstance(parsed, dict) and parsed.get("data"):
        return parsed["data"]["token"]
    return None


def main():
    print("================ C8 安全社区黑盒测试 ================")
    t1 = login("IT_STU_001")
    t2 = login("IT_STU_002")
    t3 = login("IT_STU_003")
    print(f"登录令牌: T1={'ok' if t1 else 'FAIL'} T2={'ok' if t2 else 'FAIL'} T3={'ok' if t3 else 'FAIL'}")
    assert all([t1, t2, t3]), "登录失败"

    end = (datetime.datetime.now() + datetime.timedelta(days=1)).strftime("%Y-%m-%dT%H:%M:%S")
    end3 = (datetime.datetime.now() + datetime.timedelta(days=3)).strftime("%Y-%m-%dT%H:%M:%S")

    print("\n==== C8-001 访客二维码 ====")
    # 1 申请
    code, r = check("C8-001-1 申请访客授权", "POST", "/api/visitor-authorizations", t1,
                    {"visitorName": "访客甲", "visitReason": "探访", "endTime": end}, expect_code=200)
    auth_id = r.get("data", {}).get("authorizationId") if isinstance(r, dict) else None
    # 2 查询列表
    check("C8-001-2 查询我的授权(含1条)", "GET", "/api/students/IT_STU_001/visitor-authorizations", t1, None,
          expect_code=200)
    # 3 查看凭证
    check("C8-001-3 查看授权凭证", "GET", f"/api/visitor-authorizations/{auth_id}", t1, None, expect_code=200)
    # 4 撤销
    check("C8-001-4 撤销授权", "POST", f"/api/visitor-authorizations/{auth_id}/revoke", t1, None, expect_code=200)
    # 5 重复撤销（负例）
    check("C8-001-5 重复撤销(应400)", "POST", f"/api/visitor-authorizations/{auth_id}/revoke", t1, None, expect_code=400)
    # 6 过去时间（负例）
    check("C8-001-6 截止时间过去(应400)", "POST", "/api/visitor-authorizations", t1,
          {"visitorName": "访客乙", "endTime": "2020-01-01T00:00:00"}, expect_code=400)
    # 7 越权查看他人凭证（负例）
    code2, r2 = call("POST", "/api/visitor-authorizations", t2,
                     {"visitorName": "访客丙", "endTime": end})
    auth2 = r2.get("data", {}).get("authorizationId") if isinstance(r2, dict) else None
    check("C8-001-7 越权查看他人凭证(应403)", "GET", f"/api/visitor-authorizations/{auth2}", t1, None, expect_code=403)

    print("\n==== C8-002 房间投票一人一票 ====")
    # 1 查列表
    check("C8-002-1 查询房间投票(空)", "GET", "/api/rooms/900101/votes", t1, None, expect_code=200)
    # 2 发起投票
    code, r = check("C8-002-2 发起投票", "POST", "/api/room-votes", t1,
                    {"roomId": 900101, "topic": "是否安装空调", "deadline": end3, "eligibleCount": 4}, expect_code=200)
    vote_id = r.get("data", {}).get("voteId") if isinstance(r, dict) else None
    print(f"       voteId = {vote_id}")
    # 3 T1 投同意
    check("C8-002-3 T1投同意", "POST", f"/api/room-votes/{vote_id}/responses", t1, {"choice": "同意"}, expect_code=200)
    # 4 T1 重复投（负例）
    check("C8-002-4 T1重复投(应400)", "POST", f"/api/room-votes/{vote_id}/responses", t1, {"choice": "不同意"}, expect_code=400)
    # 5 T2 投不同意
    check("C8-002-5 T2投不同意", "POST", f"/api/room-votes/{vote_id}/responses", t2, {"choice": "不同意"}, expect_code=200)
    # 6 统计
    code, r = check("C8-002-6 查看统计(1同意1不同意)", "GET", f"/api/room-votes/{vote_id}", t1, None, expect_code=200)
    # 7 非本房间学生投（负例，实现应拒绝）
    check("C8-002-7 非本房间学生投(应拒绝)", "POST", f"/api/room-votes/{vote_id}/responses", t3, {"choice": "同意"}, expect_code=403)
    # 8 非法选项（负例）
    check("C8-002-8 非法选项(应400)", "POST", f"/api/room-votes/{vote_id}/responses", t1, {"choice": "弃权"}, expect_code=400)
    # 9 不存在房间（负例）
    check("C8-002-9 不存在房间(应404)", "POST", "/api/room-votes", t1,
          {"roomId": 999999, "topic": "x", "eligibleCount": 4}, expect_code=404)

    print("\n================ 汇总 ================")
    passed = [r for r in results if r[0] == "PASS"]
    failed = [r for r in results if r[0] == "FAIL"]
    print(f"PASS {len(passed)} / FAIL {len(failed)}")
    for s, l in failed:
        print(f"  [X] {l}")


if __name__ == "__main__":
    main()
