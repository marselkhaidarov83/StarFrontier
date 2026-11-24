from pathlib import Path
import argparse
import xml.etree.ElementTree as E
import zipfile,re,math,json

ROOT=Path(__file__).resolve().parents[3]
ns={'m':'http://schemas.openxmlformats.org/spreadsheetml/2006/main'}
with zipfile.ZipFile(ROOT/'Documentation/Economy/Sources/SF_Data_v92.xlsx') as z:
    ss=[''.join(x.itertext()) for x in E.fromstring(z.read('xl/sharedStrings.xml'))] if 'xl/sharedStrings.xml' in z.namelist() else []
    rel={x.attrib['Id']:x.attrib['Target'].lstrip('/') for x in E.fromstring(z.read('xl/_rels/workbook.xml.rels'))}
    target=rel['rId50'];target=target if target.startswith('xl/') else 'xl/'+target
    rows=[]
    for row in E.fromstring(z.read(target)).findall('m:sheetData/m:row',ns):
        d={}
        for c in row:
            v=c.find('m:v',ns);v=v.text if v is not None else ''
            if c.attrib.get('t')=='s':v=ss[int(v)]
            elif c.attrib.get('t')=='inlineStr':v=''.join(c.find('m:is',ns).itertext())
            d[re.sub(r'\d','',c.attrib['r'])]=v
        rows.append(d)
def num(x):return int(float(x))
nodes={};keys={};dep={};gal={};section=''
for d in rows:
    a=d.get('A','')
    if a.startswith('Исследования'):section=a
    if not a.startswith('research_branch_'):continue
    if section=='Исследования':
        uid=num(d['C'].split(' · ')[0]);keys[(a,num(d['B']))]=uid
        nodes[uid]={'name':d['D'],'max':num(d['H']),'start':num(d['J'])}
    elif section=='Исследования / Уровни исследования':
        uid=keys[(a,num(d['B']))];level=num(d['D']);gal[(uid,level)]=num(d['E'])
    elif section=='Исследования / Уровни исследования / Зависимости':
        uid=keys[(a,num(d['B']))];level=num(d['C'])
        dep.setdefault((uid,level),[]).append((num(d['E'].split(' · ')[0]),num(d['F'])))

def run(policy='growth',daily=False,session_minutes=30,rate=1,limit_days=600,
        *, stage_price=None, credits_per_hour=0, astralites_per_hour=0,
        station_upgrade_day=None, station_upgrade_cost=(0, 0)):
    levels={i:n['start'] for i,n in nodes.items()};jobs={};count=0;milestones={};now=0
    raw_minutes=0;completions=[]
    completed_cost = [0, 0]
    spent = [0, 0]
    income = (credits_per_hour, astralites_per_hour)
    active_minutes = 0
    science_rate = rate
    station_paid = (0, 0)
    upgraded_at = None
    reserving_for_upgrade = False
    def capacity():return max(1,(levels[62]+1)//2)
    def can_afford(cost):
        return all(price <= math.floor(active_minutes * hourly / 60) - total - station
                   for price, hourly, total, station in zip(cost, income, spent, station_paid))
    def fill(t):
        nonlocal raw_minutes
        if reserving_for_upgrade:
            return
        while len(jobs)<capacity():
            candidates=[]
            for uid,n in nodes.items():
                if uid in jobs or levels[uid]>=n['max']:continue
                nxt=levels[uid]+1
                if any(levels.get(i,0)<l for i,l in dep.get((uid,nxt),[])):continue
                if policy=='no_growth' and uid in (62,63):continue
                duration=10*nxt**3*(1-.05*levels[63])
                duration=math.floor(duration+.5)
                priority=(0 if uid==62 and nxt<=9 else 1 if uid==63 else 2) if policy=='growth' else 0
                cost = stage_price(nxt) if stage_price is not None else (0, 0)
                if not can_afford(cost):
                    continue
                candidates.append((priority,duration,uid,nxt,cost))
            if not candidates:return
            _,duration,uid,nxt,cost=min(candidates)
            jobs[uid]={'left':duration,'level':nxt,'start':t,'duration':duration,'cost':cost}
            for resource, price in enumerate(cost):
                spent[resource] += price
            raw_minutes+=duration
    def advance(end,online):
        nonlocal now,count
        while now<end-1e-7:
            if online:fill(now)
            dt=min([end-now]+[j['left']/science_rate for j in jobs.values()])
            for j in jobs.values():j['left']-=dt*science_rate
            now+=dt
            done=[i for i,j in jobs.items() if j['left']<1e-6]
            for uid in done:
                j=jobs.pop(uid);levels[uid]=j['level'];count+=1
                for resource, price in enumerate(j['cost']):
                    completed_cost[resource] += price
                completions.append((now,uid,j['level']))
                if uid in (62,63):milestones[str(uid)+'_'+str(j['level'])]=round(now/1440,3)
            if count>=368:return True
        if online:fill(now)
        return count>=368
    sessions=[]
    for day in range(limit_days):
        if not daily and day%6>=4:continue
        for hour in (8,14,20):sessions.append((day*1440+hour*60,day*1440+hour*60+session_minutes))
    first=sessions[0][0];now=first;previous_exit=first
    count_at_105=count_at_150=None;completion_time=None
    for start,end in sessions:
        offline_end=min(start,previous_exit+720)
        if now<offline_end:
            if advance(offline_end,False):completion_time=now;break
        now=start
        if (station_upgrade_day is not None and upgraded_at is None
                and (start - first) / 1440 >= station_upgrade_day):
            reserving_for_upgrade = not can_afford(station_upgrade_cost)
            if not reserving_for_upgrade:
                station_paid = station_upgrade_cost
                upgraded_at = round((start - first) / 1440, 2)
                science_rate = 2
        if advance(end,True):completion_time=now;break
        active_minutes += end - start
        previous_exit=end
    return {'policy':policy,'daily':daily,'session_minutes':session_minutes,'science_rate':rate,
        'completion_day_since_first_session':round((completion_time-first)/1440,2) if completion_time else None,
        'completed':count,'capacity_at_end':capacity(),'acceleration_level':levels[63],
        'milestones_days_from_first':{k:round(v-first/1440,2) for k,v in milestones.items()},
        'active_started_minutes_sum':raw_minutes,
        'credits_paid':spent[0], 'astralites_paid':spent[1],
        'credits_completed':completed_cost[0], 'astralites_completed':completed_cost[1],
        'station_upgrade_day':station_upgrade_day, 'station_upgraded_at':upgraded_at}

def trial_stage_price(level):
    credits = 60 * level
    return credits, math.ceil(credits / 200)


def funded_scenarios():
    results = []
    for credits, astralites in ((667, 3), (667, 6), (1334, 3), (1334, 6)):
        result = run(limit_days=150, stage_price=trial_stage_price,
                     credits_per_hour=credits, astralites_per_hour=astralites)
        result.update(credits_per_active_hour=credits, astralites_per_active_hour=astralites)
        results.append(result)
    return results


def station_order_scenarios():
    results = []
    for astralites in (3, 6):
        for day in (0, 3, 7, 14, None):
            result = run(limit_days=150, stage_price=trial_stage_price,
                         credits_per_hour=1334, astralites_per_hour=astralites,
                         station_upgrade_day=day, station_upgrade_cost=(2000, 10))
            result.update(credits_per_active_hour=1334, astralites_per_active_hour=astralites)
            results.append(result)
    return results


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    modes = parser.add_mutually_exclusive_group()
    modes.add_argument('--funded', action='store_true', help='Compare scenarios with paid research stages.')
    modes.add_argument('--station-order', action='store_true', help='Compare early and delayed science station upgrades.')
    args = parser.parse_args()
    if args.station_order:
        results = station_order_scenarios()
    elif args.funded:
        results = funded_scenarios()
    else:
        results = [run('growth'),run('shortest'),run('no_growth'),run('growth',True,45),run('growth',rate=2)]
    print(json.dumps({'node_count':len(nodes),'levels':sum(n['max'] for n in nodes.values()),'start':sum(n['start'] for n in nodes.values()),'galaxy_gates':sorted(set(gal.values())),'results':results},ensure_ascii=False,indent=2))
